namespace AFH.Adviser.Application.Models.Skills;

public sealed class AdviserSkillCatalogItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? Description { get; init; }
    public bool LicenseRequired { get; init; }
    public string? Certification { get; init; }
    public int? RenewalMonths { get; init; }
    public bool IsActive { get; init; }
}
