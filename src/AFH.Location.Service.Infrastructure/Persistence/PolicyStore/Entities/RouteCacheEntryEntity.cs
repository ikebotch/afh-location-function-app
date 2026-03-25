namespace AFH.Location.Service.Infrastructure.Persistence.PolicyStore.Entities;

public sealed class RouteCacheEntryEntity
{
    public string CacheKey { get; set; } = string.Empty;
    public int EtaMinutes { get; set; }
    public double DistanceMiles { get; set; }
    public string Confidence { get; set; } = "Low";
    public DateTime ExpiresUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
