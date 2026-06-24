using AFH.Adviser.Application.Abstractions.Auth;
using AFH.Adviser.Application.Models.Auth;

namespace AFH.Adviser.Infrastructure.Persistence.Auth;

public sealed class InMemoryDomainUserContextStore : IDomainUserContextStore
{
    public Task<DomainUserContext> GetContextAsync(DomainUserIdentity identity, CancellationToken ct)
        => Task.FromResult(new DomainUserContext(
            identity.UserId,
            identity.UserId,
            identity.Email,
            identity.DisplayName,
            null,
            null,
            [],
            []));
}
