using AFH.Adviser.Application.Abstractions;
using AFH.Adviser.Infrastructure.External.Calendar;
using AFH.Adviser.Infrastructure.Options;
using AFH.Adviser.Infrastructure.Persistence.Repositories;
using AFH.Common.SharePointUtils.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;

namespace AFH.Adviser.Infrastructure.Composition;

public static class AdviserInfrastructureDependencyInjection
{
    public static IServiceCollection AddAdviserInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient();

        services.AddOptions<CalendarServiceOptions>()
            .Bind(configuration.GetSection(CalendarServiceOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), $"{CalendarServiceOptions.SectionName}:BaseUrl must be an absolute URI.")
            .Validate(options => options.ScheduleLookbackMinutes > 0, $"{CalendarServiceOptions.SectionName}:ScheduleLookbackMinutes must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<AdviserFeedOptions>()
            .Bind(configuration.GetSection(AdviserFeedOptions.SectionName))
            .Validate(options => !options.Enabled || Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), $"{AdviserFeedOptions.SectionName}:BaseUrl must be an absolute URI when the adviser feed is enabled.")
            .ValidateOnStart();

        services.Configure<SharePointAdviserOptions>(configuration.GetSection(SharePointAdviserOptions.SectionName));

        services.AddScoped<ICalendarServiceClient, CalendarServiceClient>();

        var useAdviserFeed = configuration.GetValue<bool>("AdviserFeed:Enabled");
        if (useAdviserFeed)
        {
            services.AddScoped<IAdviserSourceRepository, HttpAdviserFeedRepository>();
        }
        else
        {
            services.AddScoped<IAdviserSourceRepository, SharePointAdviserRepository>();
        }

        services.AddScoped<IAdviserReferenceCacheRepository, SqlAdviserReferenceCacheRepository>();
        // Or if in-memory was used for fallback/tests, keep it if needed, but Sql was primary.
        
        services.AddSharePoint(configuration);
        services.AddScoped<AdviserSourceRefreshCoordinator>();
        services.AddScoped<AFH.Adviser.Application.Abstractions.IAdviserCacheSyncService, AFH.Adviser.Application.Admin.AdviserCacheSyncService>();
        services.AddScoped<IAdviserRepository, CachedAdviserRepository>();
        services.AddScoped<AFH.Adviser.Application.Licences.ILicenseCatalogService, AFH.Adviser.Application.Licences.LicenseCatalogService>();

        return services;
    }
}
