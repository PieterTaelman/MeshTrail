using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Meshtrail.Core.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Application services. Mediator itself is registered in the WebApi,
    /// because the source generator that creates AddMediator only runs there.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>(ServiceLifetime.Scoped, includeInternalTypes: true);
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
