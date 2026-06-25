namespace AFH.Adviser.Application.Models.Skills;

public sealed record AdviserSkillUpsert(
    string Name,
    string? Category,
    string? Description,
    bool LicenseRequired,
    string? Certification,
    int? RenewalMonths,
    bool IsActive);
