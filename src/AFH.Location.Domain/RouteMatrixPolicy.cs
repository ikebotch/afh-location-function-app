namespace AFH.Location.Domain;

public sealed class RouteMatrixPolicy
{
    public int MaxOriginsPerCall { get; set; } = 50;
    public int MaxDestinationsPerCall { get; set; } = 50;
    public string SuccessConfidence { get; set; } = "High";
    public string FailureConfidence { get; set; } = "Low";
    public TimeSpan SuccessCacheTtl { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan FailureCacheTtl { get; set; } = TimeSpan.FromMinutes(5);
}
