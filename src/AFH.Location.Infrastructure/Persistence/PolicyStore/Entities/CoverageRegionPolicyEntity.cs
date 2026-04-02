namespace AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;

/// <summary>
/// Stores region-level coverage policy overrides.
/// </summary>
public sealed class CoverageRegionPolicyEntity
{
    public int Id { get; set; }
    public string Region { get; set; } = string.Empty;
    public double? RadiusMiles { get; set; }
    public int? MaxTravelTimeMinutes { get; set; }
}
