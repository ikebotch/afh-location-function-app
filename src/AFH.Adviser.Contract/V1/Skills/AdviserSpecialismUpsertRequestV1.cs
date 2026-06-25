namespace AFH.Adviser.Contract.V1.Skills;

public sealed record AdviserSpecialismUpsertRequestV1(
    string? Name,
    string? Category,
    string? Description,
    bool? LicenseRequired,
    string? Certification,
    int? RenewalMonths,
    bool? IsActive);
