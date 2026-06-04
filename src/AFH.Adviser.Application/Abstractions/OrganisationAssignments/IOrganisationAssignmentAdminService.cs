using AFH.Adviser.Application.Models.OrganisationAssignments;

namespace AFH.Adviser.Application.Abstractions.OrganisationAssignments;

public interface IOrganisationAssignmentAdminService
{
    Task<IReadOnlyList<OrganisationAssignment>> SearchAsync(OrganisationAssignmentSearch search, CancellationToken ct);
    Task<OrganisationAssignment> CreateAsync(OrganisationAssignmentUpsert request, CancellationToken ct);
    Task<OrganisationAssignment?> UpdateAsync(Guid id, OrganisationAssignmentUpsert request, CancellationToken ct);
    Task<bool> DisableAsync(Guid id, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}
