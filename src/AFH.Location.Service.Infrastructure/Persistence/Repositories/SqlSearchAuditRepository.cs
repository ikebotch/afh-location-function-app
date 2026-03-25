using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore.Entities;

namespace AFH.Location.Service.Infrastructure.Persistence.Repositories;

/// <summary>
/// Persists search/audit entries in SQL Server.
/// </summary>
public sealed class SqlSearchAuditRepository : ISearchAuditRepository
{
    private readonly LocationPolicyDbContext _db;

    public SqlSearchAuditRepository(LocationPolicyDbContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(SearchAuditEntry entry, CancellationToken ct)
    {
        var row = new SearchAuditRecordEntity
        {
            RequestId = entry.RequestId,
            CreatedUtc = entry.CreatedUtc,
            RequestedStartUtc = entry.RequestedStartUtc,
            DurationMinutes = entry.DurationMinutes,
            SearchHorizonMinutes = entry.SearchHorizonMinutes,
            DestinationPostcode = entry.DestinationPostcode,
            RegionsCsv = entry.RegionsCsv,
            CandidatesReturned = entry.CandidatesReturned,
            SelectedAdviserId = entry.SelectedAdviserId,
            SelectedAdviserRating = entry.SelectedAdviserRating,
            SelectedAdviserGoldStar = entry.SelectedAdviserGoldStar,
            SelectedTravelMinutes = entry.SelectedTravelMinutes,
            SelectedMaxTravelTimeMinutes = entry.SelectedMaxTravelTimeMinutes,
            SelectedCompanyBufferMinutes = entry.SelectedCompanyBufferMinutes,
            SelectedTravelBufferMinutes = entry.SelectedTravelBufferMinutes,
            SelectedOriginSource = entry.SelectedOriginSource,
            PayloadJson = string.IsNullOrWhiteSpace(entry.PayloadJson) ? "{}" : entry.PayloadJson
        };

        _db.SearchAuditRecords.Add(row);
        await _db.SaveChangesAsync(ct);
    }
}
