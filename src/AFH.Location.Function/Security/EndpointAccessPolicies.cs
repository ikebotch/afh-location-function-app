using AFH.Location.Domain;

namespace AFH.Location.Function.Security;

public static class EndpointAccessPolicies
{
    private static readonly IReadOnlyDictionary<string, EndpointAccessPolicy> Policies =
        new Dictionary<string, EndpointAccessPolicy>(StringComparer.Ordinal)
        {
            ["OpenApiV1"] = EndpointAccessPolicy.Public,
            ["ScalarUi"] = EndpointAccessPolicy.Public,
            ["LocationHealthV1"] = EndpointAccessPolicy.Public,
            ["LicenseListV1"] = EndpointAccessPolicy.InternalOnly,

            ["RouteTimeV1"] = EndpointAccessPolicy.InternalOnly,
            ["TravelCoverageV1"] = EndpointAccessPolicy.InternalOnly,
            ["SyncAdviserCacheV1"] = EndpointAccessPolicy.InternalOnly,
            ["AdviserCoverageV1"] = EndpointAccessPolicy.InternalOnly
        };

    internal static IReadOnlyCollection<string> KnownHttpFunctions => Policies.Keys.ToArray();

    public static EndpointAccessPolicy GetPolicy(string functionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        if (Policies.TryGetValue(functionName, out var policy))
            return policy;

        throw new InvalidOperationException($"No endpoint access policy is configured for HTTP function '{functionName}'.");
    }
}
