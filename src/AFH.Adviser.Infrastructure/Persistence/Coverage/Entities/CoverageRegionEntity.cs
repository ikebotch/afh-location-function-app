namespace AFH.Adviser.Infrastructure.Persistence.Coverage.Entities;

public sealed class CoverageRegionEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LeadAdviserId { get; set; }
    public string? LeadAdviserName { get; set; }
    public string Postcodes { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;
    public double CoverageRadiusMiles { get; set; }
    public int MaxTravelTimeMinutes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
    public ICollection<AdviserRegionAssignmentEntity> AdviserAssignments { get; set; } = new List<AdviserRegionAssignmentEntity>();
}
