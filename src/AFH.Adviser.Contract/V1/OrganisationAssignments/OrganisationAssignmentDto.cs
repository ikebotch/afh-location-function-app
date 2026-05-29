namespace AFH.Adviser.Contract.V1.OrganisationAssignments;

public sealed record OrganisationAssignmentDto(
    Guid Id,
    string AssignmentType,
    string DisplayName,
    string? Email,
    string? MobileNumber,
    IReadOnlyList<string> Channels,
    string Context,
    string? OrganisationId,
    string? ClientId,
    string? Region,
    string? AdviserId,
    bool IsEnabled,
    int Priority);

