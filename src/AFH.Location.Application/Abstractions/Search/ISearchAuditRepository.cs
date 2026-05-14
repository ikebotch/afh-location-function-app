namespace AFH.Location.Application.Abstractions.Search;

public interface ISearchAuditRepository
{
    Task SaveAsync(SearchAuditEntry entry, CancellationToken ct);
}
