namespace AFH.Adviser.Infrastructure.Persistence.Coverage.Entities;

public sealed class AdviserRegionAssignmentEntity
{
    public Guid Id { get; set; }
    public Guid RegionId { get; set; }
    public CoverageRegionEntity? Region { get; set; }
    public string AdviserId { get; set; } = string.Empty;
    public string AdviserName { get; set; } = string.Empty;
    public string? Role { get; set; }
    public bool IsLead { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
