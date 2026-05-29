using AFH.Location.Application.Models.Auth;

namespace AFH.Location.Application.Abstractions.Auth;

public interface IDomainUserPermissionStore
{
    Task<bool> HasPermissionAsync(
        DomainUserIdentity identity,
        string permission,
        CancellationToken ct);
}
