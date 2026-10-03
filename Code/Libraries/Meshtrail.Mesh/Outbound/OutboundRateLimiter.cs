namespace Meshtrail.Mesh.Outbound;

/// <summary>
/// Keeps at least <c>minInterval</c> between two packets we put on the air. LoRa in EU868 has a duty-cycle limit
/// and every packet is repeated by other nodes, so flooding the mesh hurts everyone. Not thread-safe: one sender.
/// </summary>
public sealed class OutboundRateLimiter(TimeSpan minInterval, TimeProvider timeProvider)
{
    private DateTimeOffset? _lastSentAt;

    /// <summary>How long to wait before the next packet may go out (zero = now).</summary>
    public TimeSpan GetDelay()
    {
        if (_lastSentAt is null)
        {
            return TimeSpan.Zero;
        }

        var wait = _lastSentAt.Value + minInterval - timeProvider.GetUtcNow();
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    /// <summary>Waits until a packet may be sent, then records that one is being sent now.</summary>
    public async Task WaitTurnAsync(CancellationToken cancellationToken)
    {
        var delay = GetDelay();
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, timeProvider, cancellationToken);
        }

        _lastSentAt = timeProvider.GetUtcNow();
    }
}
