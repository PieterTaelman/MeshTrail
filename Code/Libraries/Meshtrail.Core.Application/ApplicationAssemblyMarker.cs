namespace Meshtrail.Core.Application;

/// <summary>
/// Points Mediator and FluentValidation at this assembly so they find every handler and validator in it.
/// </summary>
public sealed class ApplicationAssemblyMarker
{
    private ApplicationAssemblyMarker()
    {
    }
}
