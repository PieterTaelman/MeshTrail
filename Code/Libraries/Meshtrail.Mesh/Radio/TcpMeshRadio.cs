using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Channels;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Meshtrail.Mesh.Framing;
using Microsoft.Extensions.Logging;

namespace Meshtrail.Mesh.Radio;

/// <summary>
/// Talks to a real node over the Meshtastic TCP stream API. The node accepts only ONE client: when the phone app
/// connects, we are kicked off and ReadAllAsync ends. Reconnecting is the caller's job.
/// </summary>
public sealed partial class TcpMeshRadio(MeshRadioOptions options, TimeProvider timeProvider, ILogger<TcpMeshRadio> logger) : IMeshRadio
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private TcpClient? _client;
    private NetworkStream? _stream;
    private Channel<FromRadio>? _inbound;
    private CancellationTokenSource? _connectionCts;
    private ITimer? _heartbeat;
    private Task _readLoop = Task.CompletedTask;

    public string Description => $"tcp://{options.Host}:{options.Port}";

    public MeshRadioState State { get; private set; } = MeshRadioState.Disconnected;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new InvalidOperationException($"No gateway host configured. Set {MeshRadioOptions.SectionName}:Host.");
        }

        await DisconnectAsync();
        State = MeshRadioState.Connecting;

        try
        {
            _client = new TcpClient { NoDelay = true };
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(options.ConnectTimeout);
                await _client.ConnectAsync(options.Host, options.Port, timeout.Token);
            }

            _stream = _client.GetStream();
            _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _inbound = System.Threading.Channels.Channel.CreateUnbounded<FromRadio>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
            _readLoop = ReadLoopAsync(_stream, _inbound.Writer, _connectionCts.Token);

            // A fresh random id per connection; the node echoes it in config_complete_id when the dump is done.
            await SendAsync(new ToRadio { WantConfigId = (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue) }, cancellationToken);

            _heartbeat = timeProvider.CreateTimer(_ => _ = SendHeartbeatAsync(), null, options.HeartbeatInterval, options.HeartbeatInterval);
            State = MeshRadioState.Connected;
            LogConnected(Description);
        }
        catch
        {
            await DisconnectAsync();
            throw;
        }
    }

    public async IAsyncEnumerable<FromRadio> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var inbound = _inbound ?? throw new InvalidOperationException("Call ConnectAsync first.");
        await foreach (var message in inbound.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    public async Task SendAsync(ToRadio message, CancellationToken cancellationToken)
    {
        var stream = _stream ?? throw new InvalidOperationException("The gateway is not connected.");
        var frame = FrameWriter.Encode(message);

        // Two writers (heartbeat timer and dispatcher) must never interleave bytes of different frames.
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await stream.WriteAsync(frame, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        State = MeshRadioState.Disconnected;

        if (_heartbeat is not null)
        {
            await _heartbeat.DisposeAsync();
            _heartbeat = null;
        }

        if (_connectionCts is not null)
        {
            await _connectionCts.CancelAsync();
        }

        _client?.Dispose();
        _client = null;
        _stream = null;

        try
        {
            await _readLoop;
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // Expected while tearing the socket down.
        }

        _connectionCts?.Dispose();
        _connectionCts = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _writeLock.Dispose();
    }

    private async Task ReadLoopAsync(NetworkStream stream, ChannelWriter<FromRadio> writer, CancellationToken cancellationToken)
    {
        var reader = new FrameReader();
        var buffer = new byte[1024];
        Exception? failure = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    failure = new IOException("The gateway closed the connection (another client, e.g. the phone app, may have connected).");
                    break;
                }

                foreach (var payload in reader.Push(buffer.AsSpan(0, read)))
                {
                    if (TryParse(payload) is { } message)
                    {
                        await writer.WriteAsync(message, cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // We are disconnecting on purpose.
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            State = MeshRadioState.Disconnected;
            writer.TryComplete(failure);
        }
    }

    private FromRadio? TryParse(byte[] payload)
    {
        try
        {
            return FromRadio.Parser.ParseFrom(payload);
        }
        catch (InvalidProtocolBufferException exception)
        {
            // One bad frame should not kill the connection; skip it.
            LogBadFrame(exception, payload.Length);
            return null;
        }
    }

    private async Task SendHeartbeatAsync()
    {
        try
        {
            await SendAsync(new ToRadio { Heartbeat = new Heartbeat() }, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // The read loop notices a dead socket too; a failed heartbeat is only worth a log line.
            LogHeartbeatFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to Meshtastic gateway {Target}")]
    private partial void LogConnected(string target);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped an unreadable frame of {Length} bytes from the gateway")]
    private partial void LogBadFrame(Exception exception, int length);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Heartbeat to the gateway failed")]
    private partial void LogHeartbeatFailed(Exception exception);
}
