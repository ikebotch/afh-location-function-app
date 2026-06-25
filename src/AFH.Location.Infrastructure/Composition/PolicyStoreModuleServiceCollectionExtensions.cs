using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Location.Infrastructure.Composition;

internal static class PolicyStoreModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddPolicyStoreModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLocationPolicyDbContext(configuration);
        services.AddScoped<IGeoCache, SqlGeoCache>();
        services.AddHostedService<LocationPolicyDbInitializer>();

        services.AddScoped<IRouteMatrixPolicyProvider, SqlRouteMatrixPolicyProvider>();
        services.AddScoped<IGeoCachePolicyProvider, SqlGeoCachePolicyProvider>();

        return services;
    }
}
