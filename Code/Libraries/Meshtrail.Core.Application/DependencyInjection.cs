using FluentValidation;
using Meshtrail.Core.Application.UseCases.Map;
using Meshtrail.Core.Application.UseCases.Mesh;
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

        // The repositories mesh handlers share, injected as one object.
        services.AddScoped<MeshNodeStores>();
        services.AddScoped<MessageAudience>();

        // Layers of the operations map; add new ones here.
        services.AddScoped<IMapLayerSource, NodesMapLayer>();
        services.AddScoped<IMapLayerSource, GatewaysMapLayer>();
        return services;
    }
}
