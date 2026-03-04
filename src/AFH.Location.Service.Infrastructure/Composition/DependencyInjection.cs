using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Services.Common;
using AFH.Location.Service.Core.Services.V1;
using AFH.Location.Service.Infrastructure.Caching;
using AFH.Location.Service.Infrastructure.External.Calendar;
using AFH.Location.Service.Infrastructure.External.Graph;
using AFH.Location.Service.Infrastructure.External.Maps.Azure;
using AFH.Location.Service.Infrastructure.External.Maps.Google;
using AFH.Location.Service.Infrastructure.Options;
using AFH.Location.Service.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Location.Service.Infrastructure.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddLocationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Http
        services.AddHttpClient();

        // ===== Selector (v1/v2) =====
        services.AddSingleton<IRequestContextAccessor, RequestContextAccessor>();
        services.AddScoped<IMapsProviderSelector, PathBasedMapsProviderSelector>();

        // ===== Calendar availability (delegated to Booking Service) =====
        services.AddOptions<CalendarServiceOptions>()
            .Configure(options =>
            {
                options.BaseUrl =
                    configuration[$"{CalendarServiceOptions.SectionName}:BaseUrl"]
                    ?? configuration["BookingCalendar:BaseUrl"]
                    ?? string.Empty;

                options.FunctionKey =
                    configuration[$"{CalendarServiceOptions.SectionName}:FunctionKey"]
                    ?? configuration["BookingCalendar:FunctionKey"];
            });

        services.AddScoped<ICalendarServiceClient, CalendarServiceClient>();
        services.AddScoped<ICalendarAvailabilityService, CalendarAvailabilityService>();

        // ===== Maps: concrete providers =====
        services.AddScoped<AzureMapsGeocodingService>();
        services.AddScoped<AzureMapsRoutingService>();
        services.AddScoped<AzureMapsRouteMatrixService>();

        services.AddScoped<GoogleMapsGeocodingService>();
        services.AddScoped<GoogleMapsRoutingService>();

        // ===== Maps: abstractions selected by request path =====
        services.AddScoped<IGeocodingService>(sp =>
            sp.GetRequiredService<IMapsProviderSelector>().IsV2Request()
                ? sp.GetRequiredService<GoogleMapsGeocodingService>()
                : sp.GetRequiredService<AzureMapsGeocodingService>());

        services.AddScoped<IRoutingService>(sp =>
            sp.GetRequiredService<IMapsProviderSelector>().IsV2Request()
                ? sp.GetRequiredService<GoogleMapsRoutingService>()
                : sp.GetRequiredService<AzureMapsRoutingService>());

        // Route matrix (Azure only for now; v2 can be added later)
        services.AddScoped<IRouteMatrixService, AzureMapsRouteMatrixService>();
        services.AddScoped<RouteMatrixCoordinator>();

        // ===== Persistence (in-memory for now) =====
        //services.AddScoped<IAdviserRepository, SharePointAdviserRepository>();
        services.Configure<SharePointAdviserOptions>(configuration.GetSection(SharePointAdviserOptions.SectionName));
        services.AddScoped<IAdviserRepository, SharePointAdviserRepository>();


        services.AddScoped<IOfficeRepository, InMemoryOfficeRepository>();

        // ===== Policies (in-memory for now) =====
        services.AddSingleton<ICoveragePolicyProvider, InMemoryCoveragePolicyProvider>();
        services.AddSingleton<IRankingPolicyProvider, InMemoryRankingPolicyProvider>();
        services.AddSingleton<IAvailabilityPolicyProvider, InMemoryAvailabilityPolicyProvider>();
        services.AddSingleton<IBaseOfficePolicyProvider, InMemoryBaseOfficePolicyProvider>();
        services.AddSingleton<IRouteMatrixPolicyProvider, InMemoryRouteMatrixPolicyProvider>();
        services.AddSingleton<IGeoCachePolicyProvider, InMemoryGeoCachePolicyProvider>();

        // ===== Caching (single registration) =====
        services.AddMemoryCache();
        services.AddSingleton<IGeoCache, InMemoryGeoCache>();
        services.AddSingleton<IAdviserGeoCache, InMemoryAdviserGeoCache>();
        services.AddGraphClient(configuration);


        // ===== Resolvers / orchestration =====
        services.AddScoped<DestinationCoordinateResolver>();
        services.AddScoped<AdviserCoordinateResolver>();
        services.AddScoped<OfficeCoordinateResolver>();
        services.AddScoped<AdviserCandidateSource>();
        services.AddScoped<RankingService>();

        // ===== Application service =====
        services.AddScoped<ILocationSearchService, LocationSearchServiceV1>();

        return services;
    }
}
