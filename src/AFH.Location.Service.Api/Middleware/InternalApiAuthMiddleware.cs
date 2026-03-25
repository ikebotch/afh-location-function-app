using AFH.Location.Service.Api.Contracts;
using AFH.Location.Service.Infrastructure.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AFH.Location.Service.Api.Middleware;

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

    public InternalApiAuthMiddleware(
        IOptions<InternalApiAuthOptions> options,
        IHostEnvironment hostEnvironment)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
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
}
