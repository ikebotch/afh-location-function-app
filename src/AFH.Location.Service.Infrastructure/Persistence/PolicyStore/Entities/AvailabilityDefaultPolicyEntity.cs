namespace AFH.Location.Service.Infrastructure.Persistence.PolicyStore.Entities;

/// <summary>
/// Stores global/default availability policy values.
/// </summary>
public sealed class AvailabilityDefaultPolicyEntity
{
    public int Id { get; set; }
    public int DefaultTravelBufferMinutes { get; set; }
    public int MaxTravelBufferMinutes { get; set; }
    public int DefaultCompanyBufferMinutes { get; set; }
    public int MaxCompanyBufferMinutes { get; set; }
    public int PreviousClientProximityMinutes { get; set; }
}
