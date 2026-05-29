using AFH.Adviser.Application.Abstractions.Auth;
using AFH.Adviser.Contract.V1.Auth;
using AFH.Location.Function.Functions.Common;
using AFH.Location.Function.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Function.Functions.V1.Adviser;

public sealed class GetCurrentUserContextFunction
{
    private readonly IDomainUserAuthorizationService _auth;
    private readonly IDomainUserContextStore _contextStore;

    public GetCurrentUserContextFunction(
        IDomainUserAuthorizationService auth,
        IDomainUserContextStore contextStore)
    {
        _auth = auth;
        _contextStore = contextStore;
    }

    [Function("Adviser_CurrentUserContextV1")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/me")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var (identity, failure) = await _auth.AuthenticateAsync(req, ct);
        if (failure is not null)
            return failure;

        var context = await _contextStore.GetContextAsync(identity!, ct);
        return await req.WriteSuccessAsync(new CurrentUserContextResponseV1(
            context.UserId,
            context.Email,
            context.DisplayName,
            context.Roles,
            context.Permissions), ct);
    }
}
