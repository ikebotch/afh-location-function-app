using AFH.Location.Application.Abstractions.Advisers;
using AFH.Location.Application.Abstractions.Licences;
using AFH.Location.Application.Admin;
using AFH.Location.Application.Licences;
using AFH.Location.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Location.Infrastructure.Composition;

internal static class LocationApplicationModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddLocationApplicationModule(this IServiceCollection services)
    {
        services.AddSingleton<AdviserSourceRefreshCoordinator>();
        services.AddScoped<ILicenseCatalogService, LicenseCatalogService>();
        services.AddScoped<IAdviserCacheSyncService, AdviserCacheSyncService>();
        services.AddScoped<IAdviserRepository, CachedAdviserRepository>();

        return services;
    }
}
