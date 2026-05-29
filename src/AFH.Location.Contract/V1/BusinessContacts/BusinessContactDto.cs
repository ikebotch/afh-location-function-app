namespace AFH.Location.Contract.V1.BusinessContacts;

public sealed record BusinessContactDto(
    Guid Id,
    string ContactType,
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

