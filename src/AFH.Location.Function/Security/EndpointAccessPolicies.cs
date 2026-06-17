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
            ["AdviserCoverageV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_CurrentUserContextV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_UpsertRoleV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_AddRolePermissionV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_AssignUserRoleV1"] = EndpointAccessPolicy.InternalOnly,
            ["Adviser_CurrentUserContextV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["OrganisationAssignmentsListV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserOrganisationAssignmentsResolveV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["OrganisationAssignmentsCreateV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["OrganisationAssignmentsUpdateV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["OrganisationAssignmentsDisableV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["OrganisationAssignmentsDeleteV1"] = EndpointAccessPolicy.UserAuthenticated
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
