namespace AFH.Location.Service.Infrastructure.Persistence.PolicyStore.Entities;

/// <summary>
/// Stores global/default coverage policy values.
/// </summary>
public sealed class CoverageDefaultPolicyEntity
{
    public int Id { get; set; }
    public double DefaultRadiusMiles { get; set; }
    public int DefaultMaxTravelTimeMinutes { get; set; }
}
