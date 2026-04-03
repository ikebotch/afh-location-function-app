using AFH.Location.Function.Middleware;
using AFH.Location.Infrastructure.Composition;
using AFH.Common.Errors.Abstractions;
using AFH.Common.Errors.ApplicationInsights.DependencyInjection;
using AFH.Common.Errors.AzureFunctions.DependencyInjection;
using AFH.Common.Errors.Email.DependencyInjection;
using AFH.Common.Errors.Email.Models;
using AFH.Common.Errors.Email.Options;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text.Json;

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
        ConfigureWorkerSerialization(services, caseInsensitivePropertyNames: true);
    })
    .Build();

host.Run();

static void ConfigureMiddlewarePipeline(dynamic app)
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
    //services.AddApplicationInsightsTelemetryWorkerService();
    services.AddAfhCommonErrorsApplicationInsights();
    services.AddAfhCommonErrorsAzureFunctions();
    services.AddAfhCommonErrorsEmail(
        BuildErrorEmailOptions(configuration, defaultSubjectPrefix),
        sp => CreateErrorEmailSender(sp, serviceName));
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
                PropertyNameCaseInsensitive = caseInsensitivePropertyNames
            });
    });
}

static ErrorEmailOptions BuildErrorEmailOptions(IConfiguration configuration, string defaultSubjectPrefix)
{
    var section = configuration.GetSection("ErrorEmail");

    return new ErrorEmailOptions
    {
        FromAddress = section["FromAddress"],
        FromDisplayName = section["FromDisplayName"],
        ToAddresses = SplitAddresses(section["ToAddresses"]),
        CcAddresses = SplitAddresses(section["CcAddresses"]),
        BccAddresses = SplitAddresses(section["BccAddresses"]),
        SubjectPrefix = string.IsNullOrWhiteSpace(section["SubjectPrefix"]) ? defaultSubjectPrefix : section["SubjectPrefix"]!,
        IncludeDetails = !bool.TryParse(section["IncludeDetails"], out var includeDetails) || includeDetails
    };
}

static IReadOnlyCollection<string> SplitAddresses(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return [];

    return value
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static Func<ErrorEmailTemplateModel, string, CancellationToken, Task> CreateErrorEmailSender(IServiceProvider serviceProvider, string serviceName)
{
    var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AFH.Common.Errors.Email");

    return (model, _, _) =>
    {
        if (model.ToAddresses.Count > 0)
        {
            logger.LogDebug(
                "Prepared handled error email notification for Service={Service} Subject={Subject} RecipientCount={RecipientCount}, but no service-local transport is configured.",
                serviceName,
                model.Subject,
                model.ToAddresses.Count);
        }

        return Task.CompletedTask;
    };
}
