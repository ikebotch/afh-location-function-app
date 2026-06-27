using AFH.Location.Infrastructure.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;

namespace AFH.Location.Function.Middleware;

public sealed class OperationAuditMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ApplicationLoggingOptions _loggingOptions;
    private readonly ILogger<OperationAuditMiddleware> _logger;

    public OperationAuditMiddleware(
        IOptions<ApplicationLoggingOptions> loggingOptions,
        ILogger<OperationAuditMiddleware> logger)
    {
        _loggingOptions = loggingOptions.Value;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();

        var sw = Stopwatch.StartNew();
        Exception? unhandled = null;
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            unhandled = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            var response = context.GetInvocationResult().Value as HttpResponseData;
            var statusCode = (int)(response?.StatusCode ?? (unhandled is null ? HttpStatusCode.OK : HttpStatusCode.InternalServerError));

            context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var cid);
            var correlationId = cid?.ToString();
            var userProfileId = Header(req, "x-afh-user-profile-id");

            try
            {
                var applicationLogSink = context.InstanceServices.GetService(typeof(IApplicationLogSink)) as IApplicationLogSink;
                if (applicationLogSink is null)
                {
                    _logger.LogWarning("Location application log sink was not available for Function={Function}.", context.FunctionDefinition.Name);
                }
                else
                {
                    await applicationLogSink.WriteAsync(new ApplicationLogEntry
                    {
                        OccurredUtc = DateTime.UtcNow,
                        Level = GetLevel(unhandled, statusCode),
                        Category = "FunctionInvocation",
                        Operation = context.FunctionDefinition.Name,
                        CorrelationId = correlationId,
                        UserId = userProfileId,
                        ContextId = context.InvocationId,
                        EventType = unhandled is null ? "InvocationCompleted" : "InvocationFailed",
                        Result = unhandled is null && statusCode < 400 ? "Success" : "Failure",
                        Message = unhandled is null
                            ? "Location function invocation completed."
                            : "Location function invocation failed.",
                        ExceptionType = unhandled?.GetType().Name,
                        ExceptionMessage = unhandled?.Message,
                        PayloadJson = ApplicationLogPayloadHelper.Serialize(new
                        {
                            Trigger = req is null ? "Function" : "Http",
                            Method = req?.Method,
                            Path = req?.Url.AbsolutePath,
                            StatusCode = statusCode,
                            DurationMs = sw.ElapsedMilliseconds,
                            AuthorizedPermission = Header(req, "x-afh-authorized-permission"),
                            Actor = new
                            {
                                UserProfileId = userProfileId,
                                ExternalSubject = Header(req, "x-afh-user-external-subject"),
                                Email = Header(req, "x-afh-user-email"),
                                DisplayName = Header(req, "x-afh-user-display-name"),
                                AdviserId = Header(req, "x-afh-user-adviser-id")
                            }
                        }, _loggingOptions)
                    }, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist location application log.");
            }

            _logger.LogInformation(
                "operation_audit service={Service} function={Function} method={Method} path={Path} status={StatusCode} durationMs={DurationMs} correlationId={CorrelationId} operationId={OperationId} errorType={ErrorType}",
                "location",
                context.FunctionDefinition.Name,
                req?.Method ?? "FUNCTION",
                req?.Url.AbsolutePath ?? context.FunctionDefinition.Name,
                statusCode,
                sw.ElapsedMilliseconds,
                correlationId,
                context.InvocationId,
                unhandled?.GetType().Name);
        }
    }

    private static string GetLevel(Exception? unhandled, int statusCode)
    {
        if (unhandled is not null || statusCode >= 500)
            return "Error";

        if (statusCode >= 400)
            return "Warning";

        return "Information";
    }

    private static string? Header(HttpRequestData? req, string name)
        => req is not null && req.Headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault()
            : null;
}
