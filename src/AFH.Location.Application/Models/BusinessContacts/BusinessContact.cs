namespace AFH.Location.Application.Models.BusinessContacts;

public sealed record BusinessContact(
    Guid Id,
    string Context,
    string ContactType,
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

