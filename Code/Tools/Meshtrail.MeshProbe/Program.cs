using System.Globalization;
using Meshtrail.Mesh;
using Meshtrail.Mesh.Radio;
using Meshtrail.MeshProbe;
using Microsoft.Extensions.Logging.Abstractions;

// Usage:
//   dotnet run --project Code/Tools/Meshtrail.MeshProbe -- --host 192.168.1.20 [--port 4403]   (node over TCP)
//   dotnet run --project Code/Tools/Meshtrail.MeshProbe -- --simulated                         (fake mesh)
//   dotnet run --project Code/Tools/Meshtrail.MeshProbe -- --mqtt [--port 1883] [--node !4d545231] [--root msh/EU_868] [--channel LongFast]
// TCP/simulated: prints the node's own info, its node database and then every packet until Ctrl+C.
// MQTT: runs a broker for gateways, prints what they publish and can send packets down. Channel keys are never printed.

// Dots in coordinates regardless of the PC's regional settings.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var options = new MeshRadioOptions();
var mqtt = false;
int? port = null;
var virtualNodeNum = MqttProbe.DefaultVirtualNodeNum;
string? root = null;
string? channel = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--host" when i + 1 < args.Length:
            options.Host = args[++i];
            break;
        case "--port" when i + 1 < args.Length:
            port = int.Parse(args[++i], CultureInfo.InvariantCulture);
            break;
        case "--simulated":
            options.Mode = MeshRadioMode.Simulated;
            break;
        case "--mqtt":
            mqtt = true;
            break;
        case "--node" when i + 1 < args.Length && NodeIds.TryParse(args[i + 1], out var nodeNum):
            virtualNodeNum = nodeNum;
            i++;
            break;
        case "--root" when i + 1 < args.Length:
            root = args[++i];
            break;
        case "--channel" when i + 1 < args.Length:
            channel = args[++i];
            break;
    }
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

if (mqtt)
{
    try
    {
        return await MqttProbe.RunAsync(port ?? 1883, virtualNodeNum, root, channel, cancellation.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Stopped.");
        return 0;
    }
}

options.Port = port ?? MeshRadioOptions.DefaultPort;
if (options.Mode == MeshRadioMode.Tcp && string.IsNullOrWhiteSpace(options.Host))
{
    Console.Error.WriteLine("Usage: Meshtrail.MeshProbe --host <node-ip> [--port 4403] | --simulated | --mqtt [--port 1883]");
    return 1;
}

await using IMeshRadio radio = options.Mode == MeshRadioMode.Simulated
    ? new SimulatedMeshRadio(options, TimeProvider.System)
    : new TcpMeshRadio(options, TimeProvider.System, NullLogger<TcpMeshRadio>.Instance);

Console.WriteLine($"Connecting to {radio.Description} ...");
await radio.ConnectAsync(cancellation.Token);
var connectedAt = DateTime.Now;
Console.WriteLine("Connected. Waiting for the config dump (Ctrl+C to stop).");

try
{
    await foreach (var message in radio.ReadAllAsync(cancellation.Token))
    {
        Console.WriteLine($"{DateTime.Now:HH:mm:ss} {PacketText.Describe(message)}");
    }

    Console.WriteLine($"{DateTime.Now:HH:mm:ss} The node closed the connection after {DateTime.Now - connectedAt:c}.");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Stopped.");
}
catch (Exception exception)
{
    // The duration helps tell causes apart: seconds = another client took over, ~15 min = missing heartbeat.
    Console.WriteLine($"{DateTime.Now:HH:mm:ss} Connection lost after {DateTime.Now - connectedAt:c}: {exception.Message}");
    return 2;
}

return 0;
