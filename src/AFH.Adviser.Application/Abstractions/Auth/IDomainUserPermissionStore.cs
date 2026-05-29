using AFH.Adviser.Application.Models.Auth;

namespace AFH.Adviser.Application.Abstractions.Auth;

public interface IDomainUserPermissionStore
{
    Task<bool> HasPermissionAsync(
        DomainUserIdentity identity,
        string permission,
        CancellationToken ct);
}
