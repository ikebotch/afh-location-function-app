namespace AFH.Location.Contract.V1.BusinessContacts;

public sealed record BusinessContactUpsertRequestV1(
    string? Context,
    string? ContactType,
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

