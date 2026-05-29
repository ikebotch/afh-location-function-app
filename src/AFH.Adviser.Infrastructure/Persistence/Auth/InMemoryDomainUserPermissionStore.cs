using AFH.Adviser.Application.Abstractions.Auth;
using AFH.Adviser.Application.Models.Auth;

namespace AFH.Adviser.Infrastructure.Persistence.Auth;

public sealed class InMemoryDomainUserPermissionStore : IDomainUserPermissionStore
{
    public Task<bool> HasPermissionAsync(DomainUserIdentity identity, string permission, CancellationToken ct)
        => Task.FromResult(false);
}
