using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Application.Services.Common;
using AFH.Location.Service.Application.Services.V1;
using AFH.Location.Service.Infrastructure.Caching;
using AFH.Location.Service.Infrastructure.External.Calendar;
using AFH.Location.Service.Infrastructure.External.Graph;
using AFH.Location.Service.Infrastructure.External.Maps;
using AFH.Location.Service.Infrastructure.External.Maps.Azure;
using AFH.Location.Service.Infrastructure.Options;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Service.Infrastructure.Persistence.Repositories;
using AFH.Location.Service.Infrastructure.Services;
using AFH.Common.SharePointUtils.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Service.Infrastructure.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddLocationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient();

        services.AddOptions<CalendarServiceOptions>()
            .Bind(configuration.GetSection(CalendarServiceOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<InternalApiAuthOptions>()
            .Bind(configuration.GetSection(InternalApiAuthOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<LocationCoverageOptions>()
            .Bind(configuration.GetSection(LocationCoverageOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<GoogleMapsOptions>()
            .Bind(configuration.GetSection(GoogleMapsOptions.SectionName))
            .Validate(options => !options.Enabled, "Maps:Google:Enabled cannot be set because the Google provider path is intentionally disabled until it is fully implemented.")
            .ValidateOnStart();

        services.AddScoped<ICalendarServiceClient, CalendarServiceClient>();
        services.AddScoped<ICalendarAvailabilityService, CalendarAvailabilityService>();
        services.AddScoped<AzureMapsGeocodingService>();
        services.AddScoped<AzureMapsRoutingService>();
        services.AddScoped<AzureMapsRouteMatrixService>();
        services.AddScoped<IGeocodingService, AzureMapsGeocodingService>();
        services.AddScoped<IRouteCache, SqlRouteCache>();
        services.AddScoped<IRoutingService>(sp => new CachedRoutingService(sp.GetRequiredService<AzureMapsRoutingService>(), sp.GetRequiredService<IRouteCache>()));
        services.AddScoped<IRouteMatrixService>(sp => new CachedRouteMatrixService(sp.GetRequiredService<AzureMapsRouteMatrixService>(), sp.GetRequiredService<IRouteCache>()));
        services.AddScoped<RouteMatrixCoordinator>();
        services.AddSingleton<IBusinessTimeZoneProvider, BusinessTimeZoneProvider>();
        services.AddSingleton<ICoveragePresentationSettings, CoveragePresentationSettings>();
        services.AddScoped<AvailabilityEvaluator>();

        services.Configure<AdviserFeedOptions>(configuration.GetSection(AdviserFeedOptions.SectionName));
        services.Configure<SharePointAdviserOptions>(configuration.GetSection(SharePointAdviserOptions.SectionName));
        var useAdviserFeed = configuration.GetValue<bool>("AdviserFeed:Enabled");
        if (useAdviserFeed)
        {
            services.AddScoped<IAdviserSourceRepository, HttpAdviserFeedRepository>();
        }
        else
        {
            services.AddScoped<IAdviserSourceRepository, SharePointAdviserRepository>();
        }

        services.AddScoped<IOfficeRepository, InMemoryOfficeRepository>();

        var policyDbConnectionString =
            configuration.GetConnectionString("LocationPolicyDb")
            ?? configuration["LocationSearch:PolicyStore:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(policyDbConnectionString))
        {
            services.AddDbContext<LocationPolicyDbContext>(options => options.UseSqlServer(policyDbConnectionString));
            services.AddScoped<ICoveragePolicyProvider, SqlCoveragePolicyProvider>();
            services.AddScoped<IAvailabilityPolicyProvider, SqlAvailabilityPolicyProvider>();
            services.AddScoped<ISearchAuditRepository, SqlSearchAuditRepository>();
            services.AddScoped<IAdviserReferenceCacheRepository, SqlAdviserReferenceCacheRepository>();
            services.AddScoped<IGeoCache, SqlGeoCache>();
            services.AddScoped<IAdviserGeoCache, SqlGeoCache>();
            services.AddHostedService<LocationPolicyDbInitializer>();
        }
        else
        {
            services.AddSingleton<ICoveragePolicyProvider, InMemoryCoveragePolicyProvider>();
            services.AddSingleton<IAvailabilityPolicyProvider, InMemoryAvailabilityPolicyProvider>();
            services.AddSingleton<ISearchAuditRepository, NoOpSearchAuditRepository>();
            services.AddSingleton<IAdviserReferenceCacheRepository, InMemoryAdviserReferenceCacheRepository>();
            services.AddSingleton<IGeoCache, InMemoryGeoCache>();
            services.AddSingleton<IAdviserGeoCache, InMemoryAdviserGeoCache>();
        }

        services.AddSingleton<IRankingPolicyProvider, InMemoryRankingPolicyProvider>();
        services.AddSingleton<IBaseOfficePolicyProvider, InMemoryBaseOfficePolicyProvider>();
        services.AddSingleton<IRouteMatrixPolicyProvider, InMemoryRouteMatrixPolicyProvider>();
        services.AddSingleton<IGeoCachePolicyProvider, InMemoryGeoCachePolicyProvider>();

        services.AddMemoryCache();
        services.AddSharePoint(configuration);

        services.AddScoped<DestinationCoordinateResolver>();
        services.AddScoped<AdviserCoordinateResolver>();
        services.AddScoped<OfficeCoordinateResolver>();
        services.AddScoped<AdviserCandidateSource>();
        services.AddScoped<RankingService>();
        services.AddScoped<ILicenseCatalogService, LicenseCatalogService>();
        services.AddScoped<IAdviserCoverageService, AdviserCoverageServiceV1>();
        services.AddScoped<IAdviserCacheSyncService, AdviserCacheSyncService>();
        services.AddScoped<IAdviserRepository, CachedAdviserRepository>();
        services.AddScoped<ILocationSearchService, LocationSearchServiceV1>();

        return services;
    }
}
