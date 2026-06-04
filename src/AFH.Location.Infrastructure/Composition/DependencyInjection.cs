using AFH.Location.Application.Services.Travel;
using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Abstractions.Travel;
using AFH.Location.Infrastructure.Caching;
using AFH.Location.Infrastructure.External.Maps;
using AFH.Location.Infrastructure.External.Maps.Azure;
using AFH.Location.Infrastructure.Logging;
using AFH.Location.Infrastructure.Options;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Infrastructure.Persistence.Repositories;
using AFH.Common.Errors.ApplicationInsights.DependencyInjection;
using AFH.Common.Errors.EntityFramework.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AFH.Location.Infrastructure.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddLocationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient();
        services.Configure<ApplicationLoggingOptions>(configuration.GetSection(ApplicationLoggingOptions.SectionName));
        services.Configure<DomainUserAuthOptions>(configuration.GetSection(DomainUserAuthOptions.SectionName));

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

        services.AddAfhCommonErrorsApplicationInsights();
        services.AddScoped<AzureMapsGeocodingService>();
        services.AddScoped<AzureMapsRoutingService>();
        services.AddScoped<AzureMapsRouteMatrixService>();
        services.AddScoped<IGeocodingService, AzureMapsGeocodingService>();
        services.AddScoped<IRouteCache, SqlRouteCache>();
        services.AddScoped<IRoutingService>(sp => new CachedRoutingService(
            sp.GetRequiredService<AzureMapsRoutingService>(),
            sp.GetRequiredService<IRouteCache>(),
            sp.GetRequiredService<ILogger<CachedRoutingService>>()));
        services.AddScoped<IRouteMatrixService>(sp => new CachedRouteMatrixService(
            sp.GetRequiredService<AzureMapsRouteMatrixService>(),
            sp.GetRequiredService<IRouteCache>(),
            sp.GetRequiredService<ILogger<CachedRouteMatrixService>>()));
        services.AddScoped<RouteMatrixCoordinator>();
        services.AddScoped<IPostcodeCoordinateResolver, PostcodeCoordinateResolver>();
        services.AddScoped<ITravelRouteOutcomeProvider, TravelRouteOutcomeProvider>();
        services.AddScoped<ITravelCoverageService, TravelCoverageService>();
        services.AddScoped<IRouteTimeService, RouteTimeService>();

        services.AddPolicyStoreModule(configuration);

        services.AddMemoryCache();
        services.AddLocationApplicationModule();
        services.AddLoggingModule();
        services.AddScoped<LocationHandledErrorTelemetryEmitter>();

        return services;
    }

    public static IServiceCollection AddLocationPolicyDbContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var policyDbConnectionString = ResolveLocationPolicyDbConnectionString(configuration);
        if (string.IsNullOrWhiteSpace(policyDbConnectionString))
        {
            throw new InvalidOperationException(
                "Missing SQL connection string for LocationPolicyDbContext. " +
                "Set ConnectionStrings:LocationPolicyDb (or LocationSearch:PolicyStore:ConnectionString).");
        }

        var hasDbContextRegistration = services.Any(descriptor => descriptor.ServiceType == typeof(LocationPolicyDbContext));
        var hasDbContextFactoryRegistration = services.Any(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<LocationPolicyDbContext>));
        if (hasDbContextRegistration && hasDbContextFactoryRegistration)
            return services;

        if (hasDbContextRegistration || hasDbContextFactoryRegistration)
        {
            throw new InvalidOperationException(
                "LocationPolicyDbContext is partially registered. " +
                "Both LocationPolicyDbContext and IDbContextFactory<LocationPolicyDbContext> are required.");
        }

        services.AddDbContext<LocationPolicyDbContext>(options => options.UseSqlServer(policyDbConnectionString));
        services.AddDbContextFactory<LocationPolicyDbContext>(
            options => options.UseSqlServer(policyDbConnectionString),
            ServiceLifetime.Scoped);
        services.AddAfhCommonErrorsEntityFramework<LocationPolicyDbContext>();

        return services;
    }

    internal static string? ResolveLocationPolicyDbConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("LocationPolicyDb")
        ?? configuration["ConnectionStrings:LocationPolicyDb"]
        ?? configuration["Values:ConnectionStrings:LocationPolicyDb"]
        ?? configuration["LocationSearch:PolicyStore:ConnectionString"]
        ?? configuration["Values:LocationSearch:PolicyStore:ConnectionString"];
}
