namespace AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;

public sealed class GeoCacheEntryEntity
{
    public string CacheKey { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
