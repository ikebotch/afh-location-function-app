using AFH.Common.Errors.EntityFramework.DependencyInjection;
using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Infrastructure.Caching;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Location.Infrastructure.Composition;

internal static class PolicyStoreModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddPolicyStoreModule(this IServiceCollection services, IConfiguration configuration)
    {
        var policyDbConnectionString = DependencyInjection.ResolveLocationPolicyDbConnectionString(configuration);

        if (!string.IsNullOrWhiteSpace(policyDbConnectionString))
        {
            services.AddDbContext<LocationPolicyDbContext>(options => options.UseSqlServer(policyDbConnectionString));
            services.AddDbContextFactory<LocationPolicyDbContext>(
                options => options.UseSqlServer(policyDbConnectionString),
                ServiceLifetime.Scoped);
            services.AddAfhCommonErrorsEntityFramework<LocationPolicyDbContext>();
            services.AddScoped<IGeoCache, SqlGeoCache>();
            services.AddHostedService<LocationPolicyDbInitializer>();
        }
        else
        {
            services.AddSingleton<IGeoCache, InMemoryGeoCache>();
        }

        services.AddSingleton<IRouteMatrixPolicyProvider, InMemoryRouteMatrixPolicyProvider>();
        services.AddSingleton<IGeoCachePolicyProvider, InMemoryGeoCachePolicyProvider>();

        return services;
    }
}
