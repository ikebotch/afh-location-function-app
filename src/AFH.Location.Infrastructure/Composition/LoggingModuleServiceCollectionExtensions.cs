using AFH.Location.Infrastructure.Logging;
using AFH.Location.Infrastructure.Options;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Infrastructure.Composition;

internal static class LoggingModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddLoggingModule(this IServiceCollection services)
    {
        services.AddScoped<DatabaseApplicationLogSink>(sp => new DatabaseApplicationLogSink(
            sp.GetService<IDbContextFactory<LocationPolicyDbContext>>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DatabaseApplicationLogSink>>()));
        services.AddScoped<ApplicationInsightsLogSink>(sp => new ApplicationInsightsLogSink(
            sp.GetService<Microsoft.ApplicationInsights.TelemetryClient>(),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ApplicationInsightsLogSink>>()));
        services.AddScoped<IApplicationLogSink>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApplicationLoggingOptions>>().Value;
            return options.Provider switch
            {
                ApplicationLogProvider.Database => sp.GetRequiredService<DatabaseApplicationLogSink>(),
                ApplicationLogProvider.ApplicationInsights => sp.GetRequiredService<ApplicationInsightsLogSink>(),
                _ => new CompositeApplicationLogSink(
                    sp.GetRequiredService<DatabaseApplicationLogSink>(),
                    sp.GetRequiredService<ApplicationInsightsLogSink>())
            };
        });

        return services;
    }
}
