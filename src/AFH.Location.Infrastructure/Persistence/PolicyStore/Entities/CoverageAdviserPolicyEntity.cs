namespace AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;

/// <summary>
/// Stores adviser-level coverage policy overrides.
/// </summary>
public sealed class CoverageAdviserPolicyEntity
{
    public int Id { get; set; }
    public string AdviserId { get; set; } = string.Empty;
    public double? RadiusMiles { get; set; }
    public int? MaxTravelTimeMinutes { get; set; }
}
