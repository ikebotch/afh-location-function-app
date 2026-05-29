using AFH.Adviser.Application.Models.Auth;

namespace AFH.Adviser.Application.Abstractions.Auth;

public interface IDomainUserContextStore
{
    Task<DomainUserContext> GetContextAsync(
        DomainUserIdentity identity,
        CancellationToken ct);
}
