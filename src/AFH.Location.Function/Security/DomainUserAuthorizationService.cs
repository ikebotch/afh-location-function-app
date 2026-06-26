using System.Net;
using AFH.Adviser.Application.Abstractions.Auth;
using AFH.Adviser.Application.Models.Auth;
using AFH.Location.Function.Functions.Common;
using AFH.Location.Function.Middleware;
using AFH.Location.Infrastructure.Options;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;

namespace AFH.Location.Function.Security;

public sealed class DomainUserAuthorizationService : IDomainUserAuthorizationService
{
    private readonly InternalApiAuthOptions _internalOptions;
    private readonly IDomainUserTokenValidator _tokenValidator;
    private readonly IDomainUserPermissionStore _permissions;

    public DomainUserAuthorizationService(
        IOptions<InternalApiAuthOptions> internalOptions,
        IDomainUserTokenValidator tokenValidator,
        IDomainUserPermissionStore permissions)
    {
        _internalOptions = internalOptions.Value;
        _tokenValidator = tokenValidator;
        _permissions = permissions;
    }

    public async Task<HttpResponseData?> AuthorizeAsync(
        HttpRequestData req,
        string permission,
        bool allowInternal,
        CancellationToken ct)
    {
        var authorization = GetAuthorizationHeader(req);

        if (allowInternal && InternalApiAuthMiddleware.ValidateAuthorization(_internalOptions.Token, authorization) is null)
            return null;

        var identityResult = await _tokenValidator.ValidateAsync(authorization, ct);
        if (identityResult.Identity is null)
            return await req.WriteFailureAsync(ToStatusCode(identityResult.Code), new { code = identityResult.Code, message = identityResult.Message }, ct);

        var allowed = await _permissions.HasPermissionAsync(identityResult.Identity, permission, ct);
        return allowed
            ? null
            : await req.WriteFailureAsync(HttpStatusCode.Forbidden, new { code = "FORBIDDEN", message = $"Permission '{permission}' is required." }, ct);
    }

    public async Task<(DomainUserIdentity? Identity, HttpResponseData? Failure)> AuthenticateAsync(
        HttpRequestData req,
        CancellationToken ct)
    {
        var identityResult = await _tokenValidator.ValidateAsync(GetAuthorizationHeader(req), ct);
        if (identityResult.Identity is null)
        {
            var failure = await req.WriteFailureAsync(ToStatusCode(identityResult.Code), new { code = identityResult.Code, message = identityResult.Message }, ct);
            return (null, failure);
        }

        return (identityResult.Identity, null);
    }

    private static HttpStatusCode ToStatusCode(string code)
        => code.Equals("AUTH_DISABLED", StringComparison.OrdinalIgnoreCase)
            || code.Equals("FORBIDDEN", StringComparison.OrdinalIgnoreCase)
                ? HttpStatusCode.Forbidden
                : HttpStatusCode.Unauthorized;

    private static string? GetAuthorizationHeader(HttpRequestData req)
        => req.Headers.TryGetValues("Authorization", out var values) ? values.FirstOrDefault() : null;
}
