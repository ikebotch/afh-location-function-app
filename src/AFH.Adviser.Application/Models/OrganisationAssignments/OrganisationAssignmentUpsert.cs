namespace AFH.Adviser.Application.Models.OrganisationAssignments;

public sealed record OrganisationAssignmentUpsert(
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
    int Priority);

