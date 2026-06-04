namespace AFH.Adviser.Contract.V1.OrganisationAssignments;

public sealed record AdviserScopedOrganisationAssignmentsResponseV1(
    string AdviserId,
    IReadOnlyList<AdviserScopedOrganisationAssignmentDto> Assignments);
