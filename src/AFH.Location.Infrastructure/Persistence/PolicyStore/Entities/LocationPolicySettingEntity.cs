namespace AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;

public sealed class LocationPolicySettingEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; }
}
