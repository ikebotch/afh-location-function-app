using AFH.Location.Application.Abstractions;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

/// <summary>
/// No-op fallback when SQL-backed auditing is not enabled.
/// </summary>
public sealed class NoOpSearchAuditRepository : ISearchAuditRepository
{
    public Task SaveAsync(SearchAuditEntry entry, CancellationToken ct) => Task.CompletedTask;
}
