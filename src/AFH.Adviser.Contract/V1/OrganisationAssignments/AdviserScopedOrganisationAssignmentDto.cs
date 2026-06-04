namespace AFH.Adviser.Contract.V1.OrganisationAssignments;

public sealed record AdviserScopedOrganisationAssignmentDto(
    Guid Id,
    string Context,
    string AssignmentType,
    string? OrganisationId,
    string? ClientId,
    string? Region,
    string? AdviserId,
    string DisplayName,
    string? Email,
    string? MobileNumber,
    IReadOnlyList<string> Channels,
    bool IsEnabled,
    int Priority,
    string MatchLevel,
    string? MatchedOrganisationId,
    string? MatchedRegion,
    string? MatchedAdviserId);
