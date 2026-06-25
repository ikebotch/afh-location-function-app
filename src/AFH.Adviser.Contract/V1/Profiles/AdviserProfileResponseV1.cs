namespace AFH.Adviser.Contract.V1.Profiles;

public sealed record AdviserProfileResponseV1(
    string AdviserId,
    string DisplayName,
    string? MailboxUserId,
    string? HomePostcode,
    string? Region,
    string? BaseOfficeId,
    string? TeamName,
    string? ManagerId,
    IReadOnlyList<string> Skills,
    double Rating,
    bool IsActive,
    bool IsBookable,
    double? CoverageRadiusMiles,
    int? MaxTravelTimeMinutes,
    DateTime LastSyncedUtc);
