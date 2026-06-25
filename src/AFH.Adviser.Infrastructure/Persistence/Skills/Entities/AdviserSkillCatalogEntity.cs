namespace AFH.Adviser.Infrastructure.Persistence.Skills.Entities;

public sealed class AdviserSkillCatalogEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool LicenseRequired { get; set; }
    public string? Certification { get; set; }
    public int? RenewalMonths { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
