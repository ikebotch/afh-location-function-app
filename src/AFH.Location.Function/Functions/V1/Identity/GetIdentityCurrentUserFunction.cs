using AFH.Location.Function.Functions.Common;
using AFH.Identity.Application.Abstractions;
using AFH.Identity.Contracts.V1.Responses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Location.Function.Functions.V1.Identity;

public sealed class GetIdentityCurrentUserFunction
{
    public const string UserBearerTokenHeaderName = "x-afh-user-token";

    private readonly IIdentityCurrentUserService _currentUserService;

    public GetIdentityCurrentUserFunction(IIdentityCurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    [Function("Identity_CurrentUserContextV1")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "internal/identity/v1/me")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var userToken = GetHeader(req, UserBearerTokenHeaderName);
        if (string.IsNullOrWhiteSpace(userToken))
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.Unauthorized,
                new { code = "AUTH_ERROR", message = $"{UserBearerTokenHeaderName} header is required." },
                ct);
        }

        var result = await _currentUserService.GetCurrentUserAsync(userToken, ct);
        if (!result.IsSuccess || result.User is null)
        {
            var status = string.Equals(result.FailureCode, "AUTH_ERROR", StringComparison.OrdinalIgnoreCase)
                ? HttpStatusCode.Unauthorized
                : HttpStatusCode.Forbidden;

            return await req.WriteFailureAsync(
                status,
                new { code = result.FailureCode ?? "FORBIDDEN", message = result.FailureMessage ?? "Unable to resolve current identity user." },
                ct);
        }

        return await req.WriteSuccessAsync(new IdentityCurrentUserResponse
        {
            UserId = result.User.UserId,
            ExternalSubject = result.User.ExternalSubject,
            Email = result.User.Email,
            DisplayName = result.User.DisplayName,
            AdviserId = result.User.AdviserId,
            JobRole = result.User.JobRole,
            TenantId = result.User.TenantId,
            Roles = result.User.Roles,
            Permissions = result.User.Permissions,
            AccessScopes = result.User.AccessScopes
                .Select(x => new IdentityAccessScopeResponse
                {
                    Area = x.Area,
                    ScopeType = x.ScopeType,
                    ScopeValue = x.ScopeValue,
                    DisplayName = x.DisplayName
                })
                .ToArray()
        }, ct);
    }

    private static string? GetHeader(HttpRequestData req, string name)
        => req.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
}
