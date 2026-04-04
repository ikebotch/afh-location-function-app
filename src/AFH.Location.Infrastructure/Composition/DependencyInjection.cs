using AFH.Location.Application.Abstractions;
using AFH.Location.Application.Services.Common;
using AFH.Location.Application.Services.V1;
using AFH.Location.Infrastructure.Caching;
using AFH.Location.Infrastructure.External.Calendar;
using AFH.Location.Infrastructure.External.Graph;
using AFH.Location.Infrastructure.External.Maps;
using AFH.Location.Infrastructure.External.Maps.Azure;
using AFH.Location.Infrastructure.Logging;
using AFH.Location.Infrastructure.Options;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Infrastructure.Persistence.Repositories;
using AFH.Location.Infrastructure.Services;
using AFH.Common.Errors.EntityFramework.DependencyInjection;
using AFH.Common.SharePointUtils.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFH.Location.Infrastructure.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddLocationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient();
        services.Configure<ApplicationLoggingOptions>(configuration.GetSection(ApplicationLoggingOptions.SectionName));

        services.AddOptions<CalendarServiceOptions>()
            .Bind(configuration.GetSection(CalendarServiceOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), $"{CalendarServiceOptions.SectionName}:BaseUrl must be an absolute URI.")
            .Validate(options => options.ScheduleLookbackMinutes > 0, $"{CalendarServiceOptions.SectionName}:ScheduleLookbackMinutes must be greater than zero.")
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<InternalApiAuthOptions>, InternalApiAuthOptionsValidator>();
        services.AddOptions<InternalApiAuthOptions>()
            .Bind(configuration.GetSection(InternalApiAuthOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<LocationCoverageOptions>()
            .Bind(configuration.GetSection(LocationCoverageOptions.SectionName))
            .Validate(options => options.AverageTravelSpeedMph > 0, $"{LocationCoverageOptions.SectionName}:AverageTravelSpeedMph must be greater than zero.")
            .ValidateOnStart();
        services.AddOptions<GoogleMapsOptions>()
            .Bind(configuration.GetSection(GoogleMapsOptions.SectionName))
            .Validate(options => !options.Enabled, "Maps:Google:Enabled cannot be set because the Google provider path is intentionally disabled until it is fully implemented.")
            .ValidateOnStart();
        services.AddOptions<AdviserFeedOptions>()
            .Bind(configuration.GetSection(AdviserFeedOptions.SectionName))
            .Validate(options => !options.Enabled || Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), $"{AdviserFeedOptions.SectionName}:BaseUrl must be an absolute URI when the adviser feed is enabled.")
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

        services.AddPolicyStoreModule(configuration);

        services.AddMemoryCache();
        services.AddSharePoint(configuration);
        services.AddSearchModule();
        services.AddLoggingModule();

        return services;
    }

    internal static string? ResolveLocationPolicyDbConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("LocationPolicyDb")
        ?? configuration["ConnectionStrings:LocationPolicyDb"]
        ?? configuration["Values:ConnectionStrings:LocationPolicyDb"]
        ?? configuration["LocationSearch:PolicyStore:ConnectionString"]
        ?? configuration["Values:LocationSearch:PolicyStore:ConnectionString"];
}
