namespace AFH.Adviser.Application.Models.OrganisationAssignments;

public sealed record AdviserScopedOrganisationAssignmentQuery(
    string AdviserId,
    string Context,
    IReadOnlyList<string> AssignmentTypes);
