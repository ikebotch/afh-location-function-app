using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Function.Security;

public interface IDomainUserAuthorizationService
{
    Task<HttpResponseData?> AuthorizeAsync(
        HttpRequestData req,
        string permission,
        bool allowInternal,
        CancellationToken ct);
}
