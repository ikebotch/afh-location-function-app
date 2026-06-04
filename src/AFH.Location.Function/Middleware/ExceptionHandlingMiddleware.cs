using AFH.Common.Errors.Abstractions;
using AFH.Common.Errors.Builders;
using AFH.Common.Errors.Mapping;
using AFH.Common.Errors.Models;
using AFH.Location.Infrastructure.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AFH.Location.Function.Middleware;

public sealed class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly ErrorRecordBuilder _errorRecordBuilder = new();
    private readonly ApplicationLoggingOptions _loggingOptions;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly LocationExceptionMapper _exceptionMapper;
    private readonly ErrorResponseBuilder _errorResponseBuilder;

    public ExceptionHandlingMiddleware(
        IOptions<ApplicationLoggingOptions> loggingOptions,
        ILogger<ExceptionHandlingMiddleware> logger,
        LocationExceptionMapper exceptionMapper,
        ErrorResponseBuilder errorResponseBuilder)
    {
        _loggingOptions = loggingOptions.Value;
        _logger = logger;
        _exceptionMapper = exceptionMapper;
        _errorResponseBuilder = errorResponseBuilder;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var req = await context.GetHttpRequestDataAsync();
            if (req is null)
                throw;

            var mapping = _exceptionMapper.TryMap(ex, CreateErrorContext(context, req));

            _logger.Log(
                mapping.Level,
                ex,
                "Location function handled exception. Function={FunctionName} FailureSource={FailureSource} FailureCode={FailureCode} Path={Path} Method={Method} CorrelationId={CorrelationId}",
                context.FunctionDefinition.Name,
                mapping.FailureSource,
                mapping.MappingResult.ErrorCode.Value,
                req.Url.AbsolutePath,
                req.Method,
                context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value) ? value?.ToString() : null);

            await WriteFailureLogAsync(context, req, mapping, ex);
            await TryWriteErrorRecordAsync(context, mapping.MappingResult);
            TryTrackHandledExceptionTelemetry(context, mapping.MappingResult);
            await TrySendHandledExceptionEmailAsync(context, mapping.MappingResult);

            context.GetInvocationResult().Value = await BuildErrorResponseAsync(
                req,
                mapping.MappingResult,
                CancellationToken.None);
        }
    }

    private async Task<HttpResponseData> BuildErrorResponseAsync(
        HttpRequestData request,
        ExceptionMappingResult mapping,
        CancellationToken ct)
    {
        var response = request.CreateResponse((System.Net.HttpStatusCode)mapping.StatusCode);
        response.Headers.Remove("Content-Type");
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");

        await JsonSerializer.SerializeAsync(response.Body, _errorResponseBuilder.Build(mapping), JsonOptions, ct);
        return response;
    }

    private Task WriteFailureLogAsync(
        FunctionContext context,
        HttpRequestData request,
        LocationExceptionMapper.LocationHandledException mapping,
        Exception exception)
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
            ? value?.ToString()
            : null;

        var applicationLogSink = context.InstanceServices.GetService(typeof(IApplicationLogSink)) as IApplicationLogSink;
        if (applicationLogSink is null)
        {
            _logger.LogWarning("Location application log sink was not available for exception handling on Path={Path}.", request.Url.AbsolutePath);
            return Task.CompletedTask;
        }

        return applicationLogSink.WriteAsync(new ApplicationLogEntry
        {
            OccurredUtc = DateTime.UtcNow,
            Level = mapping.Level == LogLevel.Error ? "Error" : "Warning",
            Category = "Exception",
            Operation = context.FunctionDefinition.Name,
            CorrelationId = correlationId,
            ContextId = context.InvocationId,
            EventType = mapping.MappingResult.ErrorCode.Value,
            Result = "Failure",
            Message = mapping.MappingResult.Message,
            ExceptionType = exception.GetType().Name,
            ExceptionMessage = exception.Message,
            PayloadJson = ApplicationLogPayloadHelper.Serialize(new
            {
                FailureSource = mapping.FailureSource,
                FailureCode = mapping.MappingResult.ErrorCode.Value,
                StatusCode = mapping.MappingResult.StatusCode,
                Path = request.Url.AbsolutePath,
                Method = request.Method,
                CorrelationId = correlationId
            }, _loggingOptions)
        }, CancellationToken.None);
    }

    private async Task TryWriteErrorRecordAsync(FunctionContext context, ExceptionMappingResult mapping)
    {
        try
        {
            var writer = context.InstanceServices.GetService(typeof(IErrorPersistenceWriter)) as IErrorPersistenceWriter;
            if (writer is null)
                return;

            var record = _errorRecordBuilder.Build(mapping);
            await writer.WriteAsync(record, CancellationToken.None);
        }
        catch (Exception persistenceEx)
        {
            _logger.LogWarning(
                persistenceEx,
                "Failed to persist handled exception error record. Function={FunctionName}",
                context.FunctionDefinition.Name);
        }
    }

    private void TryTrackHandledExceptionTelemetry(FunctionContext context, ExceptionMappingResult mapping)
    {
        try
        {
            var emitter = context.InstanceServices.GetService(typeof(LocationHandledErrorTelemetryEmitter)) as LocationHandledErrorTelemetryEmitter;
            if (emitter is null)
                return;

            var record = _errorRecordBuilder.Build(mapping);
            emitter.Track(record, context.FunctionDefinition.Name);
        }
        catch (Exception telemetryEx)
        {
            _logger.LogWarning(
                telemetryEx,
                "Failed to emit handled exception telemetry. Function={FunctionName}",
                context.FunctionDefinition.Name);
        }
    }

    private async Task TrySendHandledExceptionEmailAsync(FunctionContext context, ExceptionMappingResult mapping)
    {
        if (!LocationHandledErrorEmailPolicy.ShouldNotify(mapping))
            return;

        try
        {
            var notifier = context.InstanceServices.GetService(typeof(IErrorNotifier)) as IErrorNotifier;
            if (notifier is null)
                return;

            var record = _errorRecordBuilder.Build(mapping);
            var request = LocationHandledErrorEmailPolicy.CreateNotificationRequest(
                context.FunctionDefinition.Name,
                mapping.StatusCode,
                record);

            await notifier.NotifyAsync(request, CancellationToken.None);
        }
        catch (Exception emailEx)
        {
            _logger.LogWarning(
                emailEx,
                "Failed to send handled exception email notification. Function={FunctionName}",
                context.FunctionDefinition.Name);
        }
    }

    private static ErrorContext CreateErrorContext(FunctionContext context, HttpRequestData request)
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
            ? value?.ToString()
            : null;

        return new ErrorContext(
            TraceId: context.InvocationId,
            CorrelationId: correlationId,
            Path: request.Url.AbsolutePath,
            Method: request.Method,
            Operation: context.FunctionDefinition.Name,
            Metadata: new Dictionary<string, string?>
            {
                ["functionId"] = context.FunctionId,
                ["invocationId"] = context.InvocationId,
                ["host"] = request.Url.Host
            });
    }
}