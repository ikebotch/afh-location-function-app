namespace AFH.Adviser.Contract.V1.OrganisationAssignments;

public sealed record OrganisationAssignmentUpsertRequestV1(
    string? Context,
    string? AssignmentType,
    string? OrganisationId,
    string? ClientId,
    string? Region,
    string? AdviserId,
    string? DisplayName,
    string? Email,
    string? MobileNumber,
    IReadOnlyList<string>? Channels,
    bool? IsEnabled,
    int? Priority);

