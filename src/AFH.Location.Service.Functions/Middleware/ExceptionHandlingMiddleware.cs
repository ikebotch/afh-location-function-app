using AFH.Location.Service.Api.Contracts;
using AFH.Location.Service.Domain.Errors;
using AFH.Location.Service.Infrastructure.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace AFH.Location.Service.Api.Middleware;

public sealed class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ApplicationLoggingOptions _loggingOptions;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        IOptions<ApplicationLoggingOptions> loggingOptions,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _loggingOptions = loggingOptions.Value;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (DestinationResolveException ex)
        {
            var req = await context.GetHttpRequestDataAsync();
            if (req is null)
                throw;

            await WriteFailureLogAsync(context, req, HttpStatusCode.UnprocessableEntity, ex.Code, ex.Message, ex);

            var res = await req!.WriteFailureAsync(
                HttpStatusCode.UnprocessableEntity,
                new { code = ex.Code, message = ex.Message },
                CancellationToken.None);
            context.GetInvocationResult().Value = res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            var req = await context.GetHttpRequestDataAsync();
            if (req is null)
                throw;

            await WriteFailureLogAsync(context, req, HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "Something went wrong.", ex);

            var res = req is null
                ? null
                : await req.WriteFailureAsync(
                    HttpStatusCode.InternalServerError,
                    new { code = "INTERNAL_ERROR", message = "Something went wrong." },
                    CancellationToken.None);
            context.GetInvocationResult().Value = res;
        }
    }

    private Task WriteFailureLogAsync(
        FunctionContext context,
        HttpRequestData request,
        HttpStatusCode statusCode,
        string failureCode,
        string detail,
        Exception exception)
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.Header, out var value)
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
            Level = statusCode == HttpStatusCode.InternalServerError ? "Error" : "Warning",
            Category = "Exception",
            Operation = context.FunctionDefinition.Name,
            CorrelationId = correlationId,
            ContextId = context.InvocationId,
            EventType = failureCode,
            Result = "Failure",
            Message = detail,
            ExceptionType = exception.GetType().Name,
            ExceptionMessage = exception.Message,
            PayloadJson = ApplicationLogPayloadHelper.Serialize(new
            {
                FailureSource = nameof(ExceptionHandlingMiddleware),
                FailureCode = failureCode,
                StatusCode = (int)statusCode,
                Path = request.Url.AbsolutePath,
                Method = request.Method,
                CorrelationId = correlationId
            }, _loggingOptions)
        }, CancellationToken.None);
    }
}
