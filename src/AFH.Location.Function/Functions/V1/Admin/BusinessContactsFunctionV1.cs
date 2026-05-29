using System.Net;
using AFH.Location.Application.Abstractions.BusinessContacts;
using AFH.Location.Application.Models.BusinessContacts;
using AFH.Location.Contract.V1.BusinessContacts;
using AFH.Location.Function.Functions.Common;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Function.Functions.V1.Admin;

public sealed class BusinessContactsFunctionV1
{
    private readonly IBusinessContactDirectory _directory;

    public BusinessContactsFunctionV1(IBusinessContactDirectory directory)
    {
        _directory = directory;
    }

    [Function("BusinessContactsListV1")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/business-contacts")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var search = ToSearch(req.Url.Query);
        var contacts = await _directory.SearchAsync(search, ct);
        var response = new BusinessContactsResponseV1(contacts.Select(ToDto).ToArray());
        return await req.WriteSuccessAsync(response, ct, ApiEnvelopeExtensions.SinglePage(response.Contacts.Count));
    }

    [Function("BusinessContactsCreateV1")]
    public async Task<HttpResponseData> CreateAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/admin/business-contacts")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<BusinessContactUpsertRequestV1>(ct);
        var validation = Validate(body);
        if (validation is not null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_CONTACT", message = validation }, ct);

        var created = await _directory.CreateAsync(ToUpsert(body!), ct);
        return await req.WriteSuccessAsync(ToDto(created), ct, statusCode: HttpStatusCode.Created);
    }

    [Function("BusinessContactsUpdateV1")]
    public async Task<HttpResponseData> UpdateAsync(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "v1/admin/business-contacts/{id:guid}")]
        HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<BusinessContactUpsertRequestV1>(ct);
        var validation = Validate(body);
        if (validation is not null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_CONTACT", message = validation }, ct);

        var updated = await _directory.UpdateAsync(id, ToUpsert(body!), ct);
        return updated is null
            ? await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "CONTACT_NOT_FOUND", message = "Business contact was not found." }, ct)
            : await req.WriteSuccessAsync(ToDto(updated), ct);
    }

    [Function("BusinessContactsDisableV1")]
    public async Task<HttpResponseData> DisableAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/admin/business-contacts/{id:guid}/disable")]
        HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var disabled = await _directory.DisableAsync(id, ct);
        return disabled
            ? await req.WriteSuccessAsync(new { id, disabled = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "CONTACT_NOT_FOUND", message = "Business contact was not found." }, ct);
    }

    [Function("BusinessContactsDeleteV1")]
    public async Task<HttpResponseData> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "v1/admin/business-contacts/{id:guid}")]
        HttpRequestData req,
        Guid id,
        CancellationToken ct)
    {
        var deleted = await _directory.DeleteAsync(id, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { id, deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "CONTACT_NOT_FOUND", message = "Business contact was not found." }, ct);
    }

    private static BusinessContactSearch ToSearch(string queryString)
    {
        var query = QueryHelpers.ParseQuery(queryString);
        var roles = query.TryGetValue("roles", out var roleValues)
            ? roleValues.SelectMany(x => (x ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).ToArray()
            : [];
        var includeDisabled = query.TryGetValue("includeDisabled", out var includeDisabledValues)
            && bool.TryParse(includeDisabledValues.FirstOrDefault(), out var includeDisabledValue)
            && includeDisabledValue;

        return new BusinessContactSearch(
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

    private static string? Validate(BusinessContactUpsertRequestV1? request)
    {
        if (request is null)
            return "Request body is required.";
        if (string.IsNullOrWhiteSpace(request.Context))
            return "context is required.";
        if (string.IsNullOrWhiteSpace(request.ContactType))
            return "contactType is required.";
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return "displayName is required.";
        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.MobileNumber))
            return "email or mobileNumber is required.";
        return null;
    }

    private static BusinessContactUpsert ToUpsert(BusinessContactUpsertRequestV1 request)
        => new(
            request.Context!,
            request.ContactType!,
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

    private static BusinessContactDto ToDto(BusinessContact contact)
        => new(
            contact.Id,
            contact.ContactType,
            contact.DisplayName,
            contact.Email,
            contact.MobileNumber,
            contact.Channels,
            contact.Context,
            contact.OrganisationId,
            contact.ClientId,
            contact.Region,
            contact.AdviserId,
            contact.IsEnabled,
            contact.Priority);
}

