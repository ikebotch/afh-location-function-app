namespace AFH.Adviser.Application.Models.OrganisationAssignments;

public sealed record OrganisationAssignmentSearch(
    string? Context,
    IReadOnlyList<string> AssignmentTypes,
    string? OrganisationId,
    string? ClientId,
    string? Region,
    string? AdviserId,
    bool IncludeDisabled = false);

