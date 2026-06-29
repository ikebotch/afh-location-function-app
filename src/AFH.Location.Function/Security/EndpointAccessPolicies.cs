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
            ["AdviserAvailabilityRulesActiveV1"] = EndpointAccessPolicy.InternalOnly,
            ["AdviserAvailabilityRulesListV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserAvailabilityRulesForAdviserV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserAvailabilityRulesCreateV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserAvailabilityRulesUpdateV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserAvailabilityRulesDeleteV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserAvailabilityTimeSlotsV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["Identity_CurrentUserContextV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_ListUserProfilesV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_GetUserProfileV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_UpsertUserProfileV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_DeleteUserProfileV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_ListPermissionsV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_UpsertPermissionV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_DeletePermissionV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_ListRolesV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_GetRoleV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_UpsertRoleV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_DeleteRoleV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_AddRolePermissionV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_RemoveRolePermissionV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_ListUserRoleMappingsV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_AssignUserRoleV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_DeleteUserRoleMappingV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_ListUserPermissionMappingsV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_AssignUserPermissionV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_DeleteUserPermissionMappingV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_ListAccessScopesV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_UpsertAccessScopeV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_DeleteAccessScopeV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_ListUserAccessScopeMappingsV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_AssignUserAccessScopeV1"] = EndpointAccessPolicy.InternalOnly,
            ["Identity_DeleteUserAccessScopeMappingV1"] = EndpointAccessPolicy.InternalOnly,
            ["Adviser_CurrentUserContextV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["OrganisationAssignmentsListV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserOrganisationAssignmentsResolveV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["OrganisationAssignmentsCreateV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["OrganisationAssignmentsUpdateV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["OrganisationAssignmentsDisableV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["OrganisationAssignmentsDeleteV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["OrganisationBranchesListV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["OrganisationRegionsListV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["LocationPolicyDefaultsGetV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["LocationPolicyDefaultsPatchV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["LocationPolicyRouteMatrixGetV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["LocationPolicyRouteMatrixPatchV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["LocationPolicyGeoCacheGetV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["LocationPolicyGeoCachePatchV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserProfilesListV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserProfilesGetV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserProfilesCreateV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserProfilesUpdateV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserProfilesDisableV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserProfilesDeleteV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserSpecialismsListV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserSpecialismsCreateV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserSpecialismsUpdateV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["AdviserSpecialismsDeleteV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["CoverageRegionsListV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["CoverageRegionsGetV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["CoverageRegionsCreateV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["CoverageRegionsUpdateV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["CoverageRegionsDisableV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["CoverageRegionsDeleteV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["CoverageRegionsAssignAdviserV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["CoverageRegionsRemoveAdviserV1"] = EndpointAccessPolicy.UserAuthenticated,
            ["CoverageAreasListV1"] = EndpointAccessPolicy.UserAuthenticated
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
