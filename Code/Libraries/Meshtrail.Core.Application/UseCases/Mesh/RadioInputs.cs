namespace Meshtrail.Core.Application.UseCases.Mesh;

// Values the gateway worker received from the radio. Plain records so the Application layer does not depend on
// the protobuf library. Everything here is untrusted; the domain cleans it.

/// <summary>What a node says about itself.</summary>
public sealed record RadioUser(string? LongName, string? ShortName, string? HardwareModel, string? Role, byte[]? PublicKey);

/// <summary>A GPS fix. Time null = the node did not say when it took the fix.</summary>
public sealed record RadioPosition(double Latitude, double Longitude, int? Altitude, DateTimeOffset? Time, int PrecisionBits);

/// <summary>Battery 0–100 (101 = external power) and voltage.</summary>
public sealed record RadioTelemetry(int? BatteryLevel, double? Voltage);
