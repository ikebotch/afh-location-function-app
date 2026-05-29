using AFH.Location.Application.Abstractions.Auth;
using AFH.Location.Application.Models.Auth;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

public sealed class InMemoryDomainUserPermissionStore : IDomainUserPermissionStore
{
    public Task<bool> HasPermissionAsync(DomainUserIdentity identity, string permission, CancellationToken ct)
        => Task.FromResult(false);
}
