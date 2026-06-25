using AFH.Adviser.Application.Abstractions.Coverage;
using AFH.Adviser.Application.Models.Coverage;

namespace AFH.Adviser.Application.Services.Coverage;

public sealed class CoverageRegionAdminService : ICoverageRegionAdminService
{
    private readonly ICoverageRegionRepository _repository;

    public CoverageRegionAdminService(ICoverageRegionRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<CoverageRegion>> SearchAsync(CoverageRegionSearch search, CancellationToken ct)
        => _repository.SearchAsync(search, ct);

    public Task<CoverageRegion?> GetAsync(Guid id, CancellationToken ct)
        => _repository.GetAsync(id, ct);

    public Task<CoverageRegion> CreateAsync(CoverageRegionUpsert request, CancellationToken ct)
        => _repository.CreateAsync(request, ct);

    public Task<CoverageRegion?> UpdateAsync(Guid id, CoverageRegionUpsert request, CancellationToken ct)
        => _repository.UpdateAsync(id, request, ct);

    public Task<bool> DisableAsync(Guid id, CancellationToken ct)
        => _repository.DisableAsync(id, ct);

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        => _repository.DeleteAsync(id, ct);

    public Task<AdviserRegionAssignment> AssignAdviserAsync(AdviserRegionAssignmentUpsert request, CancellationToken ct)
        => _repository.AssignAdviserAsync(request, ct);

    public Task<bool> RemoveAdviserAsync(Guid assignmentId, CancellationToken ct)
        => _repository.RemoveAdviserAsync(assignmentId, ct);
}
