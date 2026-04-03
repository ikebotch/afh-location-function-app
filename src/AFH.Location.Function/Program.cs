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
    var settings = configuration.GetSection("ErrorEmail").Get<ErrorEmailConfiguration>() ?? new ErrorEmailConfiguration();

    return new ErrorEmailOptions
    {
        FromAddress = settings.FromAddress,
        FromDisplayName = settings.FromDisplayName,
        ToAddresses = SplitAddresses(settings.ToAddresses),
        CcAddresses = SplitAddresses(settings.CcAddresses),
        BccAddresses = SplitAddresses(settings.BccAddresses),
        SubjectPrefix = string.IsNullOrWhiteSpace(settings.SubjectPrefix) ? defaultSubjectPrefix : settings.SubjectPrefix!,
        IncludeDetails = settings.IncludeDetails ?? true
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

internal sealed class ErrorEmailConfiguration
{
    public string? FromAddress { get; init; }
    public string? FromDisplayName { get; init; }
    public string? ToAddresses { get; init; }
    public string? CcAddresses { get; init; }
    public string? BccAddresses { get; init; }
    public string? SubjectPrefix { get; init; }
    public bool? IncludeDetails { get; init; }
}
