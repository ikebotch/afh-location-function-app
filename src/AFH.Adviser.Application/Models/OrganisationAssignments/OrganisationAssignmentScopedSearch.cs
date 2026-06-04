namespace AFH.Adviser.Application.Models.OrganisationAssignments;

public sealed record OrganisationAssignmentScopedSearch(
    string Context,
    IReadOnlyList<string> AssignmentTypes,
    string AdviserId,
    string? OrganisationId,
    string? Region,
    string? ClientId = null,
    bool IncludeDisabled = false,
    bool IncludeFallback = false);
