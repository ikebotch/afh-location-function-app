using AFH.Adviser.Application.Abstractions.OrganisationAssignments;
using AFH.Adviser.Application.Models.OrganisationAssignments;

namespace AFH.Adviser.Application.Services.OrganisationAssignments;

public sealed class OrganisationAssignmentAdminService : IOrganisationAssignmentAdminService
{
    private readonly IOrganisationAssignmentDirectory _directory;

    public OrganisationAssignmentAdminService(IOrganisationAssignmentDirectory directory)
    {
        _directory = directory;
    }

    public Task<IReadOnlyList<OrganisationAssignment>> SearchAsync(OrganisationAssignmentSearch search, CancellationToken ct)
        => _directory.SearchAsync(search, ct);

    public Task<OrganisationAssignment> CreateAsync(OrganisationAssignmentUpsert request, CancellationToken ct)
        => _directory.CreateAsync(request, ct);

    public Task<OrganisationAssignment?> UpdateAsync(Guid id, OrganisationAssignmentUpsert request, CancellationToken ct)
        => _directory.UpdateAsync(id, request, ct);

    public Task<bool> DisableAsync(Guid id, CancellationToken ct)
        => _directory.DisableAsync(id, ct);

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        => _directory.DeleteAsync(id, ct);
}
