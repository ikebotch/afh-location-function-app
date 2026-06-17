using AFH.Identity.Application.Abstractions;
using AFH.Identity.Application.Models;
using AFH.Identity.Contracts.V1.Requests;
using AFH.Identity.Contracts.V1.Responses;
using AFH.Location.Function.Functions.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Location.Function.Functions.V1.Identity;

public sealed class IdentityRbacAdminFunction
{
    private readonly IIdentityRbacAdminService _admin;

    public IdentityRbacAdminFunction(IIdentityRbacAdminService admin)
    {
        _admin = admin;
    }

    [Function("Identity_UpsertRoleV1")]
    public async Task<HttpResponseData> UpsertRoleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/identity/v1/roles")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityRoleUpsertRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Role))
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.BadRequest,
                new { code = "INVALID_ROLE", message = "role is required." },
                ct);
        }

        var result = await _admin.UpsertRoleAsync(body.Role, body.Permissions ?? [], ct);
        return await req.WriteSuccessAsync(ToRoleResponse(result), ct);
    }

    [Function("Identity_AddRolePermissionV1")]
    public async Task<HttpResponseData> AddRolePermissionAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/identity/v1/roles/{role}/permissions")]
        HttpRequestData req,
        string role,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityRolePermissionRequest>(ct);
        if (string.IsNullOrWhiteSpace(role) || body is null || string.IsNullOrWhiteSpace(body.Permission))
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.BadRequest,
                new { code = "INVALID_ROLE_PERMISSION", message = "role and permission are required." },
                ct);
        }

        var result = await _admin.AddRolePermissionAsync(role, body.Permission, ct);
        return await req.WriteSuccessAsync(ToRoleResponse(result), ct);
    }

    [Function("Identity_AssignUserRoleV1")]
    public async Task<HttpResponseData> AssignUserRoleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/identity/v1/user-role-mappings")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityUserRoleMappingRequest>(ct);
        var validation = Validate(body);
        if (validation is not null)
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.BadRequest,
                new { code = "INVALID_USER_ROLE_MAPPING", message = validation },
                ct);
        }

        var result = await _admin.AssignUserRoleAsync(new IdentityUserRoleAssignment
        {
            Role = body!.Role!,
            Email = body.Email,
            ExternalRole = body.ExternalRole,
            ExternalGroupId = body.ExternalGroupId,
            IsEnabled = body.IsEnabled ?? true
        }, ct);

        return await req.WriteSuccessAsync(new IdentityUserRoleMappingResponse
        {
            MappingId = result.MappingId,
            RoleId = result.RoleId,
            Role = result.Role,
            Email = result.Email,
            ExternalRole = result.ExternalRole,
            ExternalGroupId = result.ExternalGroupId,
            IsEnabled = result.IsEnabled
        }, ct, statusCode: HttpStatusCode.Created);
    }

    [Function("Identity_AssignUserPermissionV1")]
    public async Task<HttpResponseData> AssignUserPermissionAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/identity/v1/user-permission-mappings")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<IdentityUserPermissionMappingRequest>(ct);
        var validation = Validate(body);
        if (validation is not null)
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.BadRequest,
                new { code = "INVALID_USER_PERMISSION_MAPPING", message = validation },
                ct);
        }

        var result = await _admin.AssignUserPermissionAsync(new IdentityUserPermissionAssignment
        {
            Permission = body!.Permission!,
            Email = body.Email,
            ExternalRole = body.ExternalRole,
            ExternalGroupId = body.ExternalGroupId,
            IsEnabled = body.IsEnabled ?? true
        }, ct);

        return await req.WriteSuccessAsync(new IdentityUserPermissionMappingResponse
        {
            MappingId = result.MappingId,
            Permission = result.Permission,
            Email = result.Email,
            ExternalRole = result.ExternalRole,
            ExternalGroupId = result.ExternalGroupId,
            IsEnabled = result.IsEnabled
        }, ct, statusCode: HttpStatusCode.Created);
    }

    private static string? Validate(IdentityUserRoleMappingRequest? request)
    {
        if (request is null)
            return "Request body is required.";

        if (string.IsNullOrWhiteSpace(request.Role))
            return "role is required.";

        if (string.IsNullOrWhiteSpace(request.Email)
            && string.IsNullOrWhiteSpace(request.ExternalRole)
            && string.IsNullOrWhiteSpace(request.ExternalGroupId))
        {
            return "At least one of email, externalRole, or externalGroupId is required.";
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
            && string.IsNullOrWhiteSpace(request.ExternalRole)
            && string.IsNullOrWhiteSpace(request.ExternalGroupId))
        {
            return "At least one of email, externalRole, or externalGroupId is required.";
        }

        return null;
    }

    private static IdentityRoleResponse ToRoleResponse(IdentityRoleAdminResult result) =>
        new()
        {
            RoleId = result.RoleId,
            Role = result.Role,
            Permissions = result.Permissions
        };
}
