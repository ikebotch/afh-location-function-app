using AFH.Location.Service.Api.Middleware;
using AFH.Location.Service.Infrastructure.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(app =>
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<OperationAuditMiddleware>();
        app.UseMiddleware<JwtServiceAuthMiddleware>();     // service-to-service auth (or do at APIM)
        app.UseMiddleware<ExceptionHandlingMiddleware>();
    })
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        var built = cfg.Build();
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in built.GetSection("Values").GetChildren())
            values[child.Key] = child.Value;

        if (values.Count > 0)
            cfg.AddInMemoryCollection(values);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddLocationInfrastructure(ctx.Configuration);
    })
    .Build();

host.Run();
