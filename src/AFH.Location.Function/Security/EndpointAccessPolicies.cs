using AFH.BackendPlatform;

namespace AFH.Location.Function.Security;

public static class EndpointAccessPolicies
{
    public static EndpointAccessPolicy GetPolicy(string functionName) =>
        functionName switch
        {
            "OpenApiV1" => EndpointAccessPolicy.Public,
            "ScalarUi" => EndpointAccessPolicy.Public,
            "LocationHealthV1" => EndpointAccessPolicy.Public,
            _ => EndpointAccessPolicy.InternalOnly
        };
}
