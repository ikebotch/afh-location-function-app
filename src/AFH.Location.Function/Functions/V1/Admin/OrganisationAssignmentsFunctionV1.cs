using System.Net;
using AFH.Adviser.Application.Abstractions.OrganisationAssignments;
using AFH.Adviser.Application.Models.OrganisationAssignments;
using AFH.Adviser.Application.Models.Auth;
using AFH.Adviser.Contract.V1.OrganisationAssignments;
using AFH.Location.Function.Functions.Common;
using AFH.Location.Function.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Function.Functions.V1.Admin;

public sealed class OrganisationAssignmentsFunctionV1
{
    private readonly IOrganisationAssignmentDirectory _directory;
    private readonly IDomainUserAuthorizationService _auth;

    public OrganisationAssignmentsFunctionV1(
        IOrganisationAssignmentDirectory directory,
        IDomainUserAuthorizationService auth)
    {
        _directory = directory;
        _auth = auth;
    }

    [Function("OrganisationAssignmentsListV1")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/organisation-assignments")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, OrganisationAssignmentPermissions.Read, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var search = ToSearch(req.Url.Query);
        var assignments = await _directory.SearchAsync(search, ct);
        var response = new OrganisationAssignmentsResponseV1(assignments.Select(ToDto).ToArray());
        return await req.WriteSuccessAsync(response, ct, ApiEnvelopeExtensions.SinglePage(response.Assignments.Count));
    }

    [Function("OrganisationAssignmentsCreateV1")]
    public async Task<HttpResponseData> CreateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/organisation-assignments")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, OrganisationAssignmentPermissions.Create, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<OrganisationAssignmentUpsertRequestV1>(ct);
        var validation = Validate(body);
        if (validation is not null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_ASSIGNMENT", message = validation }, ct);

        var created = await _directory.CreateAsync(ToUpsert(body!), ct);
        return await req.WriteSuccessAsync(ToDto(created), ct, statusCode: HttpStatusCode.Created);
    }

    [Function("OrganisationAssignmentsUpdateV1")]
    public async Task<HttpResponseData> UpdateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/organisation-assignments/{id:guid}")]
        HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, OrganisationAssignmentPermissions.Update, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<OrganisationAssignmentUpsertRequestV1>(ct);
        var validation = Validate(body);
        if (validation is not null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_ASSIGNMENT", message = validation }, ct);

        var updated = await _directory.UpdateAsync(id, ToUpsert(body!), ct);
        return updated is null
            ? await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "ASSIGNMENT_NOT_FOUND", message = "Organisation assignment was not found." }, ct)
            : await req.WriteSuccessAsync(ToDto(updated), ct);
    }

    [Function("OrganisationAssignmentsDisableV1")]
    public async Task<HttpResponseData> DisableAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/organisation-assignments/{id:guid}/disable")]
        HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, OrganisationAssignmentPermissions.Disable, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var disabled = await _directory.DisableAsync(id, ct);
        return disabled
            ? await req.WriteSuccessAsync(new { id, disabled = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "ASSIGNMENT_NOT_FOUND", message = "Organisation assignment was not found." }, ct);
    }

    [Function("OrganisationAssignmentsDeleteV1")]
    public async Task<HttpResponseData> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/organisation-assignments/{id:guid}")]
        HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, OrganisationAssignmentPermissions.Delete, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var deleted = await _directory.DeleteAsync(id, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { id, deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "ASSIGNMENT_NOT_FOUND", message = "Organisation assignment was not found." }, ct);
    }

    private static OrganisationAssignmentSearch ToSearch(string queryString)
    {
        var query = QueryHelpers.ParseQuery(queryString);
        var roles = query.TryGetValue("assignmentTypes", out var roleValues)
            ? roleValues.SelectMany(x => (x ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).ToArray()
            : [];
        var includeDisabled = query.TryGetValue("includeDisabled", out var includeDisabledValues)
            && bool.TryParse(includeDisabledValues.FirstOrDefault(), out var includeDisabledValue)
            && includeDisabledValue;

        return new OrganisationAssignmentSearch(
            Get(query, "context"),
            roles,
            Get(query, "organisationId"),
            Get(query, "clientId"),
            Get(query, "region"),
            Get(query, "adviserId"),
            includeDisabled);
    }

    private static string? Get(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key)
        => query.TryGetValue(key, out var value) ? value.FirstOrDefault() : null;

    private static string? Validate(OrganisationAssignmentUpsertRequestV1? request)
    {
        if (request is null)
            return "Request body is required.";
        if (string.IsNullOrWhiteSpace(request.Context))
            return "context is required.";
        if (string.IsNullOrWhiteSpace(request.AssignmentType))
            return "assignmentType is required.";
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return "displayName is required.";
        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.MobileNumber))
            return "email or mobileNumber is required.";
        return null;
    }

    private static OrganisationAssignmentUpsert ToUpsert(OrganisationAssignmentUpsertRequestV1 request)
        => new(
            request.Context!,
            request.AssignmentType!,
            request.OrganisationId,
            request.ClientId,
            request.Region,
            request.AdviserId,
            request.DisplayName!,
            request.Email,
            request.MobileNumber,
            request.Channels ?? ["Email"],
            request.IsEnabled ?? true,
            request.Priority ?? 100);

    private static OrganisationAssignmentDto ToDto(OrganisationAssignment assignment)
        => new(
            assignment.Id,
            assignment.AssignmentType,
            assignment.DisplayName,
            assignment.Email,
            assignment.MobileNumber,
            assignment.Channels,
            assignment.Context,
            assignment.OrganisationId,
            assignment.ClientId,
            assignment.Region,
            assignment.AdviserId,
            assignment.IsEnabled,
            assignment.Priority);
}
