using AFH.Location.Application.Abstractions;
using AFH.Location.Application.Services.Common;
using AFH.Location.Application.Services.V1;
using AFH.Location.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Location.Infrastructure.Composition;

internal static class SearchModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddSearchModule(this IServiceCollection services)
    {
        services.AddScoped<DestinationCoordinateResolver>();
        services.AddScoped<AdviserCoordinateResolver>();
        services.AddScoped<OfficeCoordinateResolver>();
        services.AddScoped<AdviserCandidateSource>();
        services.AddScoped<LocationSearchRoutingCoordinator>();
        services.AddScoped<LocationResponseCandidateBuilder>();
        services.AddScoped<LocationSearchAuditEntryFactory>();
        services.AddScoped<LocationSearchAuditWriter>();
        services.AddScoped<RankingService>();
        services.AddScoped<ILicenseCatalogService, LicenseCatalogService>();
        services.AddScoped<IAdviserCoverageService, AdviserCoverageService>();
        services.AddScoped<IAdviserCacheSyncService, AdviserCacheSyncService>();
        services.AddScoped<IAdviserRepository, CachedAdviserRepository>();
        services.AddScoped<ILocationSearchService, LocationSearchService>();

        return services;
    }
}
