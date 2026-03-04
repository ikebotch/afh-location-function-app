using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;

namespace AFH.Location.Service.Api.Middleware;

public sealed class JwtServiceAuthMiddleware : IFunctionsWorkerMiddleware
{
    private readonly IConfiguration _cfg;

    public JwtServiceAuthMiddleware(IConfiguration cfg) => _cfg = cfg;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {

        await next(context);
        return;


        var req = await context.GetHttpRequestDataAsync();
        if (req is null)
        {
            await next(context);
            return;
        }

        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            await next(context);
            return;
        }

        // health can be anonymous
        if (req.Url.AbsolutePath.EndsWith("/api/v1/location/health", StringComparison.OrdinalIgnoreCase) ||
            req.Url.AbsolutePath.EndsWith("/api/v2/location/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (!req.Headers.TryGetValues("Authorization", out var authHeaders))
        {
            await Reject(context, 401, "Missing Authorization header.");
            return;
        }

        var auth = authHeaders.FirstOrDefault() ?? "";
        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await Reject(context, 401, "Invalid Authorization header.");
            return;
        }

        var token = auth["Bearer ".Length..].Trim();
        var tenantId = _cfg["Auth:TenantId"];
        var audience = _cfg["Auth:Audience"];

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(audience))
        {
            await Reject(context, 500, "Auth config missing (Auth:TenantId/Auth:Audience).");
            return;
        }

        // Minimal scaffold check. In production prefer APIM JWT validation or full OIDC key validation.
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            await Reject(context, 401, "Unreadable JWT.");
            return;
        }

        var jwt = handler.ReadJwtToken(token);
        var issOk = jwt.Issuer.Contains(tenantId, StringComparison.OrdinalIgnoreCase);
        var audOk = jwt.Audiences.Contains(audience, StringComparer.OrdinalIgnoreCase);

        if (!issOk || !audOk)
        {
            await Reject(context, 403, "Token issuer/audience not allowed.");
            return;
        }

        await next(context);
    }

    private static async Task Reject(FunctionContext ctx, int code, string message)
    {
        var req = await ctx.GetHttpRequestDataAsync();
        var res = req!.CreateResponse((System.Net.HttpStatusCode)code);
        await res.WriteStringAsync(message);
        ctx.GetInvocationResult().Value = res;
    }
}
