namespace AFH.Location.Function.Security;

public enum EndpointAccessPolicy
{
    Public,
    UserAuthenticated,
    InternalOnly,
    WebhookVerified
}

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
