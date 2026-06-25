using AFH.Adviser.Application.Models.Coverage;

namespace AFH.Adviser.Application.Abstractions.Coverage;

public interface ICoverageRegionRepository
{
    Task<IReadOnlyList<CoverageRegion>> SearchAsync(CoverageRegionSearch search, CancellationToken ct);
    Task<CoverageRegion?> GetAsync(Guid id, CancellationToken ct);
    Task<CoverageRegion> CreateAsync(CoverageRegionUpsert request, CancellationToken ct);
    Task<CoverageRegion?> UpdateAsync(Guid id, CoverageRegionUpsert request, CancellationToken ct);
    Task<bool> DisableAsync(Guid id, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    Task<AdviserRegionAssignment> AssignAdviserAsync(AdviserRegionAssignmentUpsert request, CancellationToken ct);
    Task<bool> RemoveAdviserAsync(Guid assignmentId, CancellationToken ct);
}
