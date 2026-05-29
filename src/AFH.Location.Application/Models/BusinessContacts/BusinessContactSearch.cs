namespace AFH.Location.Application.Models.BusinessContacts;

public sealed record BusinessContactSearch(
    string? Context,
    IReadOnlyList<string> ContactTypes,
    string? OrganisationId,
    string? ClientId,
    string? Region,
    string? AdviserId,
    bool IncludeDisabled = false);

