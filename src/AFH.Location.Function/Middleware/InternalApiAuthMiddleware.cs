using AFH.Location.Function.Contracts;
using AFH.Location.Infrastructure.Logging;
using AFH.Location.Infrastructure.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Location.Function.Middleware;

public sealed class InternalApiAuthMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly string[] PublicPrefixes =
    [
        "/api/v1/location/health",
        "/api/openapi/",
        "/api/scalar"
    ];

    private readonly InternalApiAuthOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ApplicationLoggingOptions _loggingOptions;
    private readonly ILogger<InternalApiAuthMiddleware> _logger;

    public InternalApiAuthMiddleware(
        IOptions<InternalApiAuthOptions> options,
        IHostEnvironment hostEnvironment,
        IOptions<ApplicationLoggingOptions> loggingOptions,
        ILogger<InternalApiAuthMiddleware> logger)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _loggingOptions = loggingOptions.Value;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();
        if (req is null)
        {
            await next(context);
            return;
        }

        if (IsPublic(req.Url.AbsolutePath))
        {
            await next(context);
            return;
        }

        if (_hostEnvironment.IsDevelopment() && _options.AllowAnonymousInDevelopment)
        {
            await next(context);
            return;
        }

        var authHeader = req.Headers.TryGetValues("Authorization", out var authHeaders)
            ? authHeaders.FirstOrDefault()
            : null;

        var failure = ValidateAuthorization(_options.Token, authHeader);
        if (failure is not null)
        {
            await WriteFailureLogAsync(context, req, failure.Value.StatusCode, "AUTH_ERROR", failure.Value.Message);
            await Reject(context, (int)failure.Value.StatusCode, failure.Value.Message);
            return;
        }

        await next(context);
    }

    public static bool IsPublic(string path) =>
        PublicPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    public static (System.Net.HttpStatusCode StatusCode, string Message)? ValidateAuthorization(
        string? expectedToken,
        string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(expectedToken))
            return (System.Net.HttpStatusCode.InternalServerError, "InternalApiAuth:Token is required for protected routes.");

        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return (System.Net.HttpStatusCode.Unauthorized, "Missing Authorization header.");

        if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return (System.Net.HttpStatusCode.Unauthorized, "Invalid Authorization header.");

        var token = authorizationHeader["Bearer ".Length..].Trim();
        if (!string.Equals(token, expectedToken.Trim(), StringComparison.Ordinal))
            return (System.Net.HttpStatusCode.Forbidden, "Bearer token is invalid.");

        return null;
    }

    private static async Task Reject(FunctionContext ctx, int code, string message)
    {
        var req = await ctx.GetHttpRequestDataAsync();
        var res = await req!.WriteFailureAsync(
            (System.Net.HttpStatusCode)code,
            new { code = "AUTH_ERROR", message },
            CancellationToken.None);
        ctx.GetInvocationResult().Value = res;
    }

    private Task WriteFailureLogAsync(
        FunctionContext context,
        HttpRequestData request,
        System.Net.HttpStatusCode statusCode,
        string failureCode,
        string detail)
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.Header, out var value)
            ? value?.ToString()
            : null;

        var applicationLogSink = context.InstanceServices.GetService(typeof(IApplicationLogSink)) as IApplicationLogSink;
        if (applicationLogSink is null)
        {
            _logger.LogWarning("Location application log sink was not available for auth failure on Path={Path}.", request.Url.AbsolutePath);
            return Task.CompletedTask;
        }

        return applicationLogSink.WriteAsync(new ApplicationLogEntry
        {
            OccurredUtc = DateTime.UtcNow,
            Level = statusCode == System.Net.HttpStatusCode.InternalServerError ? "Error" : "Warning",
            Category = "Authorization",
            Operation = context.FunctionDefinition.Name,
            CorrelationId = correlationId,
            ContextId = context.InvocationId,
            EventType = failureCode,
            Result = "Failure",
            Message = detail,
            PayloadJson = ApplicationLogPayloadHelper.Serialize(new
            {
                FailureSource = nameof(InternalApiAuthMiddleware),
                FailureCode = failureCode,
                StatusCode = (int)statusCode,
                Path = request.Url.AbsolutePath,
                Method = request.Method,
                CorrelationId = correlationId
            }, _loggingOptions)
        }, CancellationToken.None);
    }
}