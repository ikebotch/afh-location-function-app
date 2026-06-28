using AFH.Identity.Application.Abstractions;
using AFH.Identity.Application.Models;
using AFH.Identity.Contracts.V1.Requests;
using AFH.Identity.Contracts.V1.Responses;
using AFH.Location.Function.Docs.V1;
using AFH.Location.Function.Functions.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Location.Function.Functions.V1.Identity;

[LocationOpenApiTag("Identity")]
public sealed class IdentityRbacAdminFunction
{
    private readonly IIdentityRbacAdminService _admin;

    public IdentityRbacAdminFunction(IIdentityRbacAdminService admin)
    {
        _admin = admin;
    }

    [Function("Identity_ListUserProfilesV1")]
    [LocationOpenApiOperation("Identity", "List identity user profiles", ResponseType = typeof(IReadOnlyList<IdentityUserProfileResponse>))]
    public async Task<HttpResponseData> ListUserProfilesAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "internal/identity/v1/user-profiles")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var results = await _admin.ListUserProfilesAsync(ct);
        return await req.WriteSuccessAsync(results.Select(ToUserProfileResponse).ToArray(), ct);
    }

    [Function("Identity_GetUserProfileV1")]
    [LocationOpenApiOperation("Identity", "Get an identity user profile", ResponseType = typeof(IdentityUserProfileResponse))]
    public async Task<HttpResponseData> GetUserProfileAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "internal/identity/v1/user-profiles/{userProfileId:guid}")]
        HttpRequestData req,
        Guid userProfileId,
        CancellationToken ct)
    {
        var result = await _admin.GetUserProfileAsync(userProfileId, ct);
        return result is null
            ? await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "USER_PROFILE_NOT_FOUND", message = "User profile was not found." }, ct)
            : await req.WriteSuccessAsync(ToUserProfileResponse(result), ct);
    }

    [Function("Identity_UpsertUserProfileV1")]
    [LocationOpenApiOperation("Identity", "Create or update an identity user profile",
        RequestBodyType = typeof(IdentityUserProfileUpsertRequest),
        ResponseType = typeof(IdentityUserProfileResponse))]
    public async Task<HttpResponseData> UpsertUserProfileAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/identity/v1/user-profiles")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityUserProfileUpsertRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Email))
            return await BadRequest(req, "INVALID_USER_PROFILE", "email is required.", ct);

        var result = await _admin.UpsertUserProfileAsync(new IdentityUserProfileUpsert
        {
            ExternalSubject = body.ExternalSubject,
            Email = body.Email!,
            DisplayName = body.DisplayName,
            AdviserId = body.AdviserId,
            JobRole = body.JobRole,
            Status = body.Status
        }, ct);

        return await req.WriteSuccessAsync(ToUserProfileResponse(result), ct);
    }

    [Function("Identity_DeleteUserProfileV1")]
    [LocationOpenApiOperation("Identity", "Delete an identity user profile", ResponseType = typeof(object))]
    public async Task<HttpResponseData> DeleteUserProfileAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "internal/identity/v1/user-profiles/{userProfileId:guid}")]
        HttpRequestData req,
        Guid userProfileId,
        CancellationToken ct)
    {
        var deleted = await _admin.DeleteUserProfileAsync(userProfileId, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "USER_PROFILE_NOT_FOUND", message = "User profile was not found." }, ct);
    }

    [Function("Identity_ListPermissionsV1")]
    [LocationOpenApiOperation("Identity", "List permissions", ResponseType = typeof(IReadOnlyList<IdentityPermissionResponse>))]
    public async Task<HttpResponseData> ListPermissionsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "internal/identity/v1/permissions")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var results = await _admin.ListPermissionsAsync(ct);
        return await req.WriteSuccessAsync(results.Select(ToPermissionResponse).ToArray(), ct);
    }

    [Function("Identity_UpsertPermissionV1")]
    [LocationOpenApiOperation("Identity", "Create or update a permission",
        RequestBodyType = typeof(IdentityPermissionUpsertRequest),
        ResponseType = typeof(IdentityPermissionResponse))]
    public async Task<HttpResponseData> UpsertPermissionAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/identity/v1/permissions")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityPermissionUpsertRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Permission))
            return await BadRequest(req, "INVALID_PERMISSION", "permission is required.", ct);

        var result = await _admin.UpsertPermissionAsync(new IdentityPermissionUpsert
        {
            Permission = body.Permission!,
            DisplayName = body.DisplayName,
            Description = body.Description,
            Category = body.Category,
            IsEnabled = body.IsEnabled ?? true
        }, ct);
        return await req.WriteSuccessAsync(ToPermissionResponse(result), ct);
    }

    [Function("Identity_DeletePermissionV1")]
    [LocationOpenApiOperation("Identity", "Delete a permission", ResponseType = typeof(object))]
    public async Task<HttpResponseData> DeletePermissionAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "internal/identity/v1/permissions/{permissionId:guid}")]
        HttpRequestData req,
        Guid permissionId,
        CancellationToken ct)
    {
        var deleted = await _admin.DeletePermissionAsync(permissionId, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "PERMISSION_NOT_FOUND", message = "Permission was not found." }, ct);
    }

    [Function("Identity_ListRolesV1")]
    [LocationOpenApiOperation("Identity", "List roles", ResponseType = typeof(IReadOnlyList<IdentityRoleResponse>))]
    public async Task<HttpResponseData> ListRolesAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "internal/identity/v1/roles")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var results = await _admin.ListRolesAsync(ct);
        return await req.WriteSuccessAsync(results.Select(ToRoleResponse).ToArray(), ct);
    }

    [Function("Identity_GetRoleV1")]
    [LocationOpenApiOperation("Identity", "Get a role", ResponseType = typeof(IdentityRoleResponse))]
    public async Task<HttpResponseData> GetRoleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "internal/identity/v1/roles/{roleId:guid}")]
        HttpRequestData req,
        Guid roleId,
        CancellationToken ct)
    {
        var result = await _admin.GetRoleAsync(roleId, ct);
        return result is null
            ? await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "ROLE_NOT_FOUND", message = "Role was not found." }, ct)
            : await req.WriteSuccessAsync(ToRoleResponse(result), ct);
    }

    [Function("Identity_UpsertRoleV1")]
    [LocationOpenApiOperation("Identity", "Create or update a role",
        RequestBodyType = typeof(IdentityRoleUpsertRequest),
        ResponseType = typeof(IdentityRoleResponse))]
    public async Task<HttpResponseData> UpsertRoleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/identity/v1/roles")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityRoleUpsertRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Role))
            return await BadRequest(req, "INVALID_ROLE", "role is required.", ct);

        var result = await _admin.UpsertRoleAsync(body.Role, body.Permissions ?? [], ct);
        return await req.WriteSuccessAsync(ToRoleResponse(result), ct);
    }

    [Function("Identity_DeleteRoleV1")]
    [LocationOpenApiOperation("Identity", "Delete a role", ResponseType = typeof(object))]
    public async Task<HttpResponseData> DeleteRoleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "internal/identity/v1/roles/{roleId:guid}")]
        HttpRequestData req,
        Guid roleId,
        CancellationToken ct)
    {
        var deleted = await _admin.DeleteRoleAsync(roleId, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "ROLE_NOT_FOUND", message = "Role was not found." }, ct);
    }

    [Function("Identity_AddRolePermissionV1")]
    [LocationOpenApiOperation("Identity", "Add a permission to a role",
        RequestBodyType = typeof(IdentityRolePermissionRequest),
        ResponseType = typeof(IdentityRoleResponse))]
    public async Task<HttpResponseData> AddRolePermissionAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/identity/v1/roles/{role}/permissions")]
        HttpRequestData req,
        string role,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityRolePermissionRequest>(ct);
        if (string.IsNullOrWhiteSpace(role) || body is null || string.IsNullOrWhiteSpace(body.Permission))
            return await BadRequest(req, "INVALID_ROLE_PERMISSION", "role and permission are required.", ct);

        var result = await _admin.AddRolePermissionAsync(role, body.Permission, ct);
        return await req.WriteSuccessAsync(ToRoleResponse(result), ct);
    }

    [Function("Identity_RemoveRolePermissionV1")]
    [LocationOpenApiOperation("Identity", "Remove a permission from a role", ResponseType = typeof(object))]
    public async Task<HttpResponseData> RemoveRolePermissionAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "internal/identity/v1/roles/{role}/permissions/{permission}")]
        HttpRequestData req,
        string role,
        string permission,
        CancellationToken ct)
    {
        var deleted = await _admin.RemoveRolePermissionAsync(role, permission, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "ROLE_PERMISSION_NOT_FOUND", message = "Role permission mapping was not found." }, ct);
    }

    [Function("Identity_ListUserRoleMappingsV1")]
    [LocationOpenApiOperation("Identity", "List user role mappings", ResponseType = typeof(IReadOnlyList<IdentityUserRoleMappingResponse>))]
    public async Task<HttpResponseData> ListUserRoleMappingsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "internal/identity/v1/user-role-mappings")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var results = await _admin.ListUserRoleMappingsAsync(ct);
        return await req.WriteSuccessAsync(results.Select(ToUserRoleMappingResponse).ToArray(), ct);
    }

    [Function("Identity_AssignUserRoleV1")]
    [LocationOpenApiOperation("Identity", "Assign a role to a user, external app role, or external group",
        RequestBodyType = typeof(IdentityUserRoleMappingRequest),
        ResponseType = typeof(IdentityUserRoleMappingResponse),
        SuccessStatusCode = HttpStatusCode.Created)]
    public async Task<HttpResponseData> AssignUserRoleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/identity/v1/user-role-mappings")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityUserRoleMappingRequest>(ct);
        var validation = Validate(body);
        if (validation is not null)
            return await BadRequest(req, "INVALID_USER_ROLE_MAPPING", validation, ct);

        var result = await _admin.AssignUserRoleAsync(new IdentityUserRoleAssignment
        {
            Role = body!.Role!,
            UserProfileId = body.UserProfileId,
            Email = body.Email,
            ExternalRole = body.ExternalRole,
            ExternalGroupId = body.ExternalGroupId,
            IsEnabled = body.IsEnabled ?? true
        }, ct);

        return await req.WriteSuccessAsync(ToUserRoleMappingResponse(result), ct, statusCode: HttpStatusCode.Created);
    }

    [Function("Identity_DeleteUserRoleMappingV1")]
    [LocationOpenApiOperation("Identity", "Delete a user role mapping", ResponseType = typeof(object))]
    public async Task<HttpResponseData> DeleteUserRoleMappingAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "internal/identity/v1/user-role-mappings/{mappingId:guid}")]
        HttpRequestData req,
        Guid mappingId,
        CancellationToken ct)
    {
        var deleted = await _admin.DeleteUserRoleMappingAsync(mappingId, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "USER_ROLE_MAPPING_NOT_FOUND", message = "User role mapping was not found." }, ct);
    }

    [Function("Identity_ListUserPermissionMappingsV1")]
    [LocationOpenApiOperation("Identity", "List user permission mappings", ResponseType = typeof(IReadOnlyList<IdentityUserPermissionMappingResponse>))]
    public async Task<HttpResponseData> ListUserPermissionMappingsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "internal/identity/v1/user-permission-mappings")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var results = await _admin.ListUserPermissionMappingsAsync(ct);
        return await req.WriteSuccessAsync(results.Select(ToUserPermissionMappingResponse).ToArray(), ct);
    }

    [Function("Identity_AssignUserPermissionV1")]
    [LocationOpenApiOperation("Identity", "Grant or deny a permission for a user",
        RequestBodyType = typeof(IdentityUserPermissionMappingRequest),
        ResponseType = typeof(IdentityUserPermissionMappingResponse),
        SuccessStatusCode = HttpStatusCode.Created)]
    public async Task<HttpResponseData> AssignUserPermissionAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/identity/v1/user-permission-mappings")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityUserPermissionMappingRequest>(ct);
        var validation = Validate(body);
        if (validation is not null)
            return await BadRequest(req, "INVALID_USER_PERMISSION_MAPPING", validation, ct);

        var result = await _admin.AssignUserPermissionAsync(new IdentityUserPermissionAssignment
        {
            Permission = body!.Permission!,
            UserProfileId = body.UserProfileId,
            ExternalSubject = body.ExternalSubject,
            Email = body.Email,
            IsGranted = body.IsGranted ?? true,
            IsEnabled = body.IsEnabled ?? true,
            Reason = body.Reason
        }, ct);

        return await req.WriteSuccessAsync(ToUserPermissionMappingResponse(result), ct, statusCode: HttpStatusCode.Created);
    }

    [Function("Identity_DeleteUserPermissionMappingV1")]
    [LocationOpenApiOperation("Identity", "Delete a user permission mapping", ResponseType = typeof(object))]
    public async Task<HttpResponseData> DeleteUserPermissionMappingAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "internal/identity/v1/user-permission-mappings/{mappingId:guid}")]
        HttpRequestData req,
        Guid mappingId,
        CancellationToken ct)
    {
        var deleted = await _admin.DeleteUserPermissionMappingAsync(mappingId, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "USER_PERMISSION_MAPPING_NOT_FOUND", message = "User permission mapping was not found." }, ct);
    }

    [Function("Identity_ListAccessScopesV1")]
    [LocationOpenApiOperation("Identity", "List access scope catalogue", ResponseType = typeof(IReadOnlyList<IdentityAccessScopeAdminResponse>))]
    public async Task<HttpResponseData> ListAccessScopesAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "internal/identity/v1/access-scopes")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var results = await _admin.ListAccessScopesAsync(ct);
        return await req.WriteSuccessAsync(results.Select(ToAccessScopeAdminResponse).ToArray(), ct);
    }

    [Function("Identity_UpsertAccessScopeV1")]
    [LocationOpenApiOperation("Identity", "Create or update an access scope",
        RequestBodyType = typeof(IdentityAccessScopeUpsertRequest),
        ResponseType = typeof(IdentityAccessScopeAdminResponse))]
    public async Task<HttpResponseData> UpsertAccessScopeAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/identity/v1/access-scopes")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityAccessScopeUpsertRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Area) || string.IsNullOrWhiteSpace(body.ScopeType))
            return await BadRequest(req, "INVALID_ACCESS_SCOPE", "area and scopeType are required.", ct);

        var result = await _admin.UpsertAccessScopeAsync(new IdentityAccessScopeUpsert
        {
            AccessScopeId = body.AccessScopeId,
            Area = body.Area!,
            ScopeType = body.ScopeType!,
            ScopeValue = body.ScopeValue,
            DisplayName = body.DisplayName,
            Description = body.Description,
            IsEnabled = body.IsEnabled ?? true
        }, ct);

        return await req.WriteSuccessAsync(ToAccessScopeAdminResponse(result), ct);
    }

    [Function("Identity_DeleteAccessScopeV1")]
    [LocationOpenApiOperation("Identity", "Delete an access scope", ResponseType = typeof(object))]
    public async Task<HttpResponseData> DeleteAccessScopeAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "internal/identity/v1/access-scopes/{accessScopeId:guid}")]
        HttpRequestData req,
        Guid accessScopeId,
        CancellationToken ct)
    {
        var deleted = await _admin.DeleteAccessScopeAsync(accessScopeId, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "ACCESS_SCOPE_NOT_FOUND", message = "Access scope was not found." }, ct);
    }

    [Function("Identity_ListUserAccessScopeMappingsV1")]
    [LocationOpenApiOperation("Identity", "List user access scope mappings", ResponseType = typeof(IReadOnlyList<IdentityUserAccessScopeMappingResponse>))]
    public async Task<HttpResponseData> ListUserAccessScopeMappingsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "internal/identity/v1/user-access-scopes")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var results = await _admin.ListUserAccessScopeMappingsAsync(ct);
        return await req.WriteSuccessAsync(results.Select(ToUserAccessScopeMappingResponse).ToArray(), ct);
    }

    [Function("Identity_AssignUserAccessScopeV1")]
    [LocationOpenApiOperation("Identity", "Assign an access scope to a user",
        RequestBodyType = typeof(IdentityUserAccessScopeMappingRequest),
        ResponseType = typeof(IdentityUserAccessScopeMappingResponse),
        SuccessStatusCode = HttpStatusCode.Created)]
    public async Task<HttpResponseData> AssignUserAccessScopeAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/identity/v1/user-access-scopes")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityUserAccessScopeMappingRequest>(ct);
        var validation = Validate(body);
        if (validation is not null)
            return await BadRequest(req, "INVALID_USER_ACCESS_SCOPE_MAPPING", validation, ct);

        var result = await _admin.AssignUserAccessScopeAsync(new IdentityUserAccessScopeAssignment
        {
            UserProfileId = body!.UserProfileId,
            AccessScopeId = body.AccessScopeId,
            ExternalSubject = body.ExternalSubject,
            Email = body.Email,
            Area = body.Area ?? string.Empty,
            ScopeType = body.ScopeType ?? string.Empty,
            ScopeValue = body.ScopeValue,
            DisplayName = body.DisplayName,
            IsEnabled = body.IsEnabled ?? true
        }, ct);

        return await req.WriteSuccessAsync(ToUserAccessScopeMappingResponse(result), ct, statusCode: HttpStatusCode.Created);
    }

    [Function("Identity_DeleteUserAccessScopeMappingV1")]
    [LocationOpenApiOperation("Identity", "Delete a user access scope mapping", ResponseType = typeof(object))]
    public async Task<HttpResponseData> DeleteUserAccessScopeMappingAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "internal/identity/v1/user-access-scopes/{mappingId:guid}")]
        HttpRequestData req,
        Guid mappingId,
        CancellationToken ct)
    {
        var deleted = await _admin.DeleteUserAccessScopeMappingAsync(mappingId, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "USER_ACCESS_SCOPE_MAPPING_NOT_FOUND", message = "User access scope mapping was not found." }, ct);
    }

    private static string? Validate(IdentityUserRoleMappingRequest? request)
    {
        if (request is null)
            return "Request body is required.";

        if (string.IsNullOrWhiteSpace(request.Role))
            return "role is required.";

        if (string.IsNullOrWhiteSpace(request.Email)
            && request.UserProfileId is null
            && string.IsNullOrWhiteSpace(request.ExternalRole)
            && string.IsNullOrWhiteSpace(request.ExternalGroupId))
        {
            return "At least one of userProfileId, email, externalRole, or externalGroupId is required.";
        }

        return null;
    }

    private static string? Validate(IdentityUserPermissionMappingRequest? request)
    {
        if (request is null)
            return "Request body is required.";

        if (string.IsNullOrWhiteSpace(request.Permission))
            return "permission is required.";

        if (string.IsNullOrWhiteSpace(request.Email)
            && request.UserProfileId is null
            && string.IsNullOrWhiteSpace(request.ExternalSubject))
        {
            return "At least one of userProfileId, email, or externalSubject is required.";
        }

        return null;
    }

    private static string? Validate(IdentityUserAccessScopeMappingRequest? request)
    {
        if (request is null)
            return "Request body is required.";

        if (request.AccessScopeId is null && string.IsNullOrWhiteSpace(request.Area))
            return "area is required.";

        if (request.AccessScopeId is null && string.IsNullOrWhiteSpace(request.ScopeType))
            return "scopeType is required.";

        if (string.IsNullOrWhiteSpace(request.Email)
            && request.UserProfileId is null
            && string.IsNullOrWhiteSpace(request.ExternalSubject))
        {
            return "At least one of userProfileId, email, or externalSubject is required.";
        }

        return null;
    }

    private static Task<HttpResponseData> BadRequest(HttpRequestData req, string code, string message, CancellationToken ct) =>
        req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code, message }, ct);

    private static IdentityUserProfileResponse ToUserProfileResponse(IdentityUserProfileResult result) =>
        new()
        {
            UserProfileId = result.UserProfileId,
            ExternalSubject = result.ExternalSubject,
            Email = result.Email,
            DisplayName = result.DisplayName,
            AdviserId = result.AdviserId,
            JobRole = result.JobRole,
            Status = result.Status
        };

    private static IdentityPermissionResponse ToPermissionResponse(IdentityPermissionResult result) =>
        new()
        {
            PermissionId = result.PermissionId,
            Permission = result.Permission,
            DisplayName = result.DisplayName,
            Description = result.Description,
            Category = result.Category,
            IsEnabled = result.IsEnabled
        };

    private static IdentityRoleResponse ToRoleResponse(IdentityRoleAdminResult result) =>
        new()
        {
            RoleId = result.RoleId,
            Role = result.Role,
            Permissions = result.Permissions
        };

    private static IdentityUserRoleMappingResponse ToUserRoleMappingResponse(IdentityUserRoleMappingResult result) =>
        new()
        {
            MappingId = result.MappingId,
            UserProfileId = result.UserProfileId,
            RoleId = result.RoleId,
            Role = result.Role,
            Email = result.Email,
            ExternalRole = result.ExternalRole,
            ExternalGroupId = result.ExternalGroupId,
            IsEnabled = result.IsEnabled
        };

    private static IdentityUserPermissionMappingResponse ToUserPermissionMappingResponse(IdentityUserPermissionMappingResult result) =>
        new()
        {
            MappingId = result.MappingId,
            UserProfileId = result.UserProfileId,
            PermissionId = result.PermissionId,
            Permission = result.Permission,
            ExternalSubject = result.ExternalSubject,
            Email = result.Email,
            IsGranted = result.IsGranted,
            IsEnabled = result.IsEnabled,
            Reason = result.Reason
        };

    private static IdentityAccessScopeAdminResponse ToAccessScopeAdminResponse(IdentityAccessScopeAdminResult result) =>
        new()
        {
            AccessScopeId = result.AccessScopeId,
            Area = result.Area,
            ScopeType = result.ScopeType,
            ScopeValue = result.ScopeValue,
            DisplayName = result.DisplayName,
            Description = result.Description,
            IsEnabled = result.IsEnabled
        };

    private static IdentityUserAccessScopeMappingResponse ToUserAccessScopeMappingResponse(IdentityUserAccessScopeMappingResult result) =>
        new()
        {
            MappingId = result.MappingId,
            UserProfileId = result.UserProfileId,
            AccessScopeId = result.AccessScopeId,
            ExternalSubject = result.ExternalSubject,
            Email = result.Email,
            Area = result.Area,
            ScopeType = result.ScopeType,
            ScopeValue = result.ScopeValue,
            DisplayName = result.DisplayName,
            IsEnabled = result.IsEnabled
        };
}
