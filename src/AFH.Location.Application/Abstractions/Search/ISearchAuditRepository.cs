namespace AFH.Location.Application.Abstractions;

public interface ISearchAuditRepository
{
    Task SaveAsync(SearchAuditEntry entry, CancellationToken ct);
}
