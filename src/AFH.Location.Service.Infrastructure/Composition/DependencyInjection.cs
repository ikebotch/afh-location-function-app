using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Services.Common;
using AFH.Location.Service.Core.Services.V1;
using AFH.Location.Service.Infrastructure.Caching;
using AFH.Location.Service.Infrastructure.External.Calendar;
using AFH.Location.Service.Infrastructure.External.Graph;
using AFH.Location.Service.Infrastructure.External.Maps.Azure;
using AFH.Location.Service.Infrastructure.External.Maps.Google;
using AFH.Location.Service.Infrastructure.Options;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Service.Infrastructure.Persistence.Repositories;
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

        services.AddSingleton<IRequestContextAccessor, RequestContextAccessor>();
        services.AddScoped<IMapsProviderSelector, PathBasedMapsProviderSelector>();

        services.AddOptions<CalendarServiceOptions>()
            .Bind(configuration.GetSection(CalendarServiceOptions.SectionName))
            .ValidateOnStart();

        services.AddScoped<ICalendarServiceClient, CalendarServiceClient>();
        services.AddScoped<ICalendarAvailabilityService, CalendarAvailabilityService>();

        services.AddScoped<AzureMapsGeocodingService>();
        services.AddScoped<AzureMapsRoutingService>();
        services.AddScoped<AzureMapsRouteMatrixService>();

        services.AddScoped<GoogleMapsGeocodingService>();
        services.AddScoped<GoogleMapsRoutingService>();

        services.AddScoped<IGeocodingService>(sp =>
            sp.GetRequiredService<IMapsProviderSelector>().IsV2Request()
                ? sp.GetRequiredService<GoogleMapsGeocodingService>()
                : sp.GetRequiredService<AzureMapsGeocodingService>());

        services.AddScoped<IRoutingService>(sp =>
            sp.GetRequiredService<IMapsProviderSelector>().IsV2Request()
                ? sp.GetRequiredService<GoogleMapsRoutingService>()
                : sp.GetRequiredService<AzureMapsRoutingService>());

        services.AddScoped<IRouteMatrixService, AzureMapsRouteMatrixService>();
        services.AddScoped<RouteMatrixCoordinator>();

        services.Configure<SharePointAdviserOptions>(configuration.GetSection(SharePointAdviserOptions.SectionName));
        services.AddScoped<IAdviserRepository, SharePointAdviserRepository>();

        services.AddScoped<IOfficeRepository, InMemoryOfficeRepository>();

        var policyDbConnectionString =
            configuration.GetConnectionString("LocationPolicyDb")
            ?? configuration["LocationSearch:PolicyStore:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(policyDbConnectionString))
        {
            services.AddDbContext<LocationPolicyDbContext>(options =>
                options.UseSqlServer(policyDbConnectionString));

            services.AddScoped<ICoveragePolicyProvider, SqlCoveragePolicyProvider>();
            services.AddScoped<IAvailabilityPolicyProvider, SqlAvailabilityPolicyProvider>();
            services.AddScoped<ISearchAuditRepository, SqlSearchAuditRepository>();
            services.AddHostedService<LocationPolicyDbInitializer>();
        }
        else
        {
            services.AddSingleton<ICoveragePolicyProvider, InMemoryCoveragePolicyProvider>();
            services.AddSingleton<IAvailabilityPolicyProvider, InMemoryAvailabilityPolicyProvider>();
            services.AddSingleton<ISearchAuditRepository, NoOpSearchAuditRepository>();
        }

        services.AddSingleton<IRankingPolicyProvider, InMemoryRankingPolicyProvider>();
        services.AddSingleton<IBaseOfficePolicyProvider, InMemoryBaseOfficePolicyProvider>();
        services.AddSingleton<IRouteMatrixPolicyProvider, InMemoryRouteMatrixPolicyProvider>();
        services.AddSingleton<IGeoCachePolicyProvider, InMemoryGeoCachePolicyProvider>();

        services.AddMemoryCache();
        services.AddSingleton<IGeoCache, InMemoryGeoCache>();
        services.AddSingleton<IAdviserGeoCache, InMemoryAdviserGeoCache>();
        services.AddGraphClient(configuration);

        services.AddScoped<DestinationCoordinateResolver>();
        services.AddScoped<AdviserCoordinateResolver>();
        services.AddScoped<OfficeCoordinateResolver>();
        services.AddScoped<AdviserCandidateSource>();
        services.AddScoped<RankingService>();

        services.AddScoped<ILocationSearchService, LocationSearchServiceV1>();

        return services;
    }
}
