using AFH.Adviser.Application.Models.OrganisationAssignments;

namespace AFH.Adviser.Application.Abstractions.OrganisationAssignments;

public interface IAdviserScopedOrganisationAssignmentResolver
{
    Task<AdviserScopedOrganisationAssignmentResolution> ResolveAsync(
        AdviserScopedOrganisationAssignmentQuery query,
        CancellationToken ct);
}
