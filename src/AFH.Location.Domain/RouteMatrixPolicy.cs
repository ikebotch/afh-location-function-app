namespace AFH.Location.Domain;

public sealed class RouteMatrixPolicy
{
    public int MaxOriginsPerCall { get; set; } = 50;
    public string SuccessConfidence { get; set; } = "High";
    public string FailureConfidence { get; set; } = "Low";
}