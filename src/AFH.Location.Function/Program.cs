using AFH.Common.Errors.Abstractions;
using AFH.Common.Errors.AzureFunctions.DependencyInjection;
using AFH.Location.Function.Middleware;
using AFH.Location.Infrastructure.Composition;
using AFH.Adviser.Infrastructure.Composition;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Text.Json.Serialization;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(app =>
    {
        ConfigureMiddlewarePipeline(app);
    })
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        ConfigureAppConfiguration(cfg);
    })
    .ConfigureServices((ctx, services) =>
    {
        AddSharedErrorHandling(services, ctx.Configuration, "[AFH Location Error]", "location");
        services.AddLocationInfrastructure(ctx.Configuration);
        services.AddAdviserInfrastructure(ctx.Configuration);
        ConfigureWorkerSerialization(services, caseInsensitivePropertyNames: true);
    })
    .Build();

host.Run();

static void ConfigureMiddlewarePipeline(IFunctionsWorkerApplicationBuilder app)
{
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<OperationAuditMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<InternalApiAuthMiddleware>();
}

static void ConfigureAppConfiguration(IConfigurationBuilder cfg)
{
    cfg
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    AddFlattenedValuesSection(cfg);
}

static void AddFlattenedValuesSection(IConfigurationBuilder cfg)
{
    var built = cfg.Build();
    var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    foreach (var child in built.GetSection("Values").GetChildren())
        values[child.Key] = child.Value;

    if (values.Count > 0)
        cfg.AddInMemoryCollection(values);
}

static void AddSharedErrorHandling(
    IServiceCollection services,
    IConfiguration configuration,
    string defaultSubjectPrefix,
    string serviceName)
{
    services.AddApplicationInsightsTelemetryWorkerService();
    services.AddAfhCommonErrorsAzureFunctions();
    services.AddLocationErrorNotificationModule(configuration, defaultSubjectPrefix, serviceName);
    services.AddSingleton<LocationExceptionMapper>();
    services.AddSingleton<IExceptionMapper>(sp => sp.GetRequiredService<LocationExceptionMapper>());

}

static void ConfigureWorkerSerialization(IServiceCollection services, bool caseInsensitivePropertyNames)
{
    services.Configure<WorkerOptions>(options =>
    {
        options.Serializer = new JsonObjectSerializer(
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = caseInsensitivePropertyNames,
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            });
    });
}
