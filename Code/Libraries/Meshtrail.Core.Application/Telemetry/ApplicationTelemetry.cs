using System.Diagnostics;

namespace Meshtrail.Core.Application.Telemetry;

/// <summary>
/// Trace source for the Application layer. ServiceDefaults listens to every "Meshtrail.*" source,
/// so spans started here show up in the Aspire dashboard without extra setup.
/// </summary>
public static class ApplicationTelemetry
{
    public const string SourceName = "Meshtrail.Application";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
