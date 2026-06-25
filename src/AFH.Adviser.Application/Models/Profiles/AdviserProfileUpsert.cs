namespace AFH.Adviser.Application.Models.Profiles;

public sealed record AdviserProfileUpsert(
    string AdviserId,
    string DisplayName,
    string? MailboxUserId,
    string? HomePostcode,
    string? Region,
    string? BaseOfficeId,
    string? TeamName,
    string? ManagerId,
    IReadOnlyCollection<string> Skills,
    double Rating,
    bool IsActive,
    bool IsBookable,
    double? CoverageRadiusMiles,
    int? MaxTravelTimeMinutes);
