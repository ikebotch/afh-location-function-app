namespace AFH.Adviser.Application.Models.OrganisationAssignments;

public sealed record OrganisationAssignment(
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
    DateTime CreatedUtc,
    DateTime? UpdatedUtc);

