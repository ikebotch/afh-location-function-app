namespace AFH.Adviser.Contract.V1.Skills;

public sealed record AdviserSpecialismResponseV1(
    Guid Id,
    string Name,
    string? Category,
    string? Description,
    bool LicenseRequired,
    string? Certification,
    int? RenewalMonths,
    bool IsActive);
