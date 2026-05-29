using Microsoft.Azure.Functions.Worker.Http;
using AFH.Adviser.Application.Models.Auth;

namespace AFH.Location.Function.Security;

public interface IDomainUserAuthorizationService
{
    Task<HttpResponseData?> AuthorizeAsync(
        HttpRequestData req,
        string permission,
        bool allowInternal,
        CancellationToken ct);

    Task<(DomainUserIdentity? Identity, HttpResponseData? Failure)> AuthenticateAsync(
        HttpRequestData req,
        CancellationToken ct);
}
