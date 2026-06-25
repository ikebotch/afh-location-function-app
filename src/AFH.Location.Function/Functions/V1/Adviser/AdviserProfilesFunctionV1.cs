using System.Net;
using AFH.Adviser.Application.Abstractions.Profiles;
using AFH.Adviser.Application.Models.Auth;
using AFH.Adviser.Application.Models.Profiles;
using AFH.Adviser.Contract.V1.Profiles;
using AFH.Location.Function.Docs.V1;
using AFH.Location.Function.Functions.Common;
using AFH.Location.Function.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Entities = AFH.Adviser.Domain.Entities;

namespace AFH.Location.Function.Functions.V1.Adviser;

public sealed class AdviserProfilesFunctionV1
{
    private readonly IAdviserProfileAdminService _profiles;
    private readonly IDomainUserAuthorizationService _auth;

    public AdviserProfilesFunctionV1(IAdviserProfileAdminService profiles, IDomainUserAuthorizationService auth)
    {
        _profiles = profiles;
        _auth = auth;
    }

    [Function("AdviserProfilesListV1")]
    [LocationOpenApiOperation("Admin", "List adviser profiles",
        Description = "Returns adviser profiles owned by the Location adviser service.",
        ResponseType = typeof(AdviserProfilesResponseV1))]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/advisers")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, AdviserAdminPermissions.Read, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var profiles = await _profiles.SearchAsync(ToSearch(req.Url.Query), ct);
        var response = new AdviserProfilesResponseV1(profiles.Select(ToDto).ToArray());
        return await req.WriteSuccessAsync(response, ct, ApiEnvelopeExtensions.SinglePage(response.Advisers.Count));
    }

    [Function("AdviserProfilesGetV1")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/advisers/{adviserId}")]
        HttpRequestData req,
        string adviserId,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, AdviserAdminPermissions.Read, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var profile = await _profiles.GetAsync(adviserId, ct);
        return profile is null
            ? await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "ADVISER_NOT_FOUND", message = "Adviser profile was not found." }, ct)
            : await req.WriteSuccessAsync(ToDto(profile), ct);
    }

    [Function("AdviserProfilesCreateV1")]
    public async Task<HttpResponseData> CreateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/advisers")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, AdviserAdminPermissions.Create, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<AdviserProfileUpsertRequestV1>(ct);
        var validation = Validate(body, requireAdviserId: true);
        if (validation is not null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_ADVISER_PROFILE", message = validation }, ct);

        var created = await _profiles.UpsertAsync(ToUpsert(body!, body!.AdviserId!), ct);
        return await req.WriteSuccessAsync(ToDto(created), ct, statusCode: HttpStatusCode.Created);
    }

    [Function("AdviserProfilesUpdateV1")]
    public async Task<HttpResponseData> UpdateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/advisers/{adviserId}")]
        HttpRequestData req,
        string adviserId,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, AdviserAdminPermissions.Update, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<AdviserProfileUpsertRequestV1>(ct);
        var validation = Validate(body, requireAdviserId: false);
        if (validation is not null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_ADVISER_PROFILE", message = validation }, ct);

        var updated = await _profiles.UpsertAsync(ToUpsert(body!, adviserId), ct);
        return await req.WriteSuccessAsync(ToDto(updated), ct);
    }

    [Function("AdviserProfilesDisableV1")]
    public async Task<HttpResponseData> DisableAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/advisers/{adviserId}/disable")]
        HttpRequestData req,
        string adviserId,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, AdviserAdminPermissions.Disable, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var disabled = await _profiles.DisableAsync(adviserId, ct);
        return disabled
            ? await req.WriteSuccessAsync(new { adviserId, disabled = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "ADVISER_NOT_FOUND", message = "Adviser profile was not found." }, ct);
    }

    [Function("AdviserProfilesDeleteV1")]
    public async Task<HttpResponseData> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/advisers/{adviserId}")]
        HttpRequestData req,
        string adviserId,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, AdviserAdminPermissions.Delete, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var deleted = await _profiles.DeleteAsync(adviserId, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { adviserId, deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "ADVISER_NOT_FOUND", message = "Adviser profile was not found." }, ct);
    }

    private static AdviserProfileSearch ToSearch(string queryString)
    {
        var query = QueryHelpers.ParseQuery(queryString);
        return new AdviserProfileSearch(
            Get(query, "search"),
            Get(query, "region"),
            GetBool(query, "includeInactive"));
    }

    private static AdviserProfileUpsert ToUpsert(AdviserProfileUpsertRequestV1 body, string adviserId)
        => new(
            adviserId.Trim(),
            body.DisplayName!.Trim(),
            Normalize(body.MailboxUserId),
            Normalize(body.HomePostcode),
            Normalize(body.Region),
            Normalize(body.BaseOfficeId),
            Normalize(body.TeamName),
            Normalize(body.ManagerId),
            (body.Skills ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            body.Rating.GetValueOrDefault(),
            body.IsActive.GetValueOrDefault(true),
            body.IsBookable.GetValueOrDefault(true),
            body.CoverageRadiusMiles,
            body.MaxTravelTimeMinutes);

    private static string? Validate(AdviserProfileUpsertRequestV1? body, bool requireAdviserId)
    {
        if (body is null)
            return "Request body is required.";
        if (requireAdviserId && string.IsNullOrWhiteSpace(body.AdviserId))
            return "Adviser id is required.";
        if (string.IsNullOrWhiteSpace(body.DisplayName))
            return "Display name is required.";
        if (body.Rating is < 0)
            return "Rating cannot be negative.";
        if (body.CoverageRadiusMiles is < 0)
            return "Coverage radius cannot be negative.";
        if (body.MaxTravelTimeMinutes is < 0)
            return "Max travel time cannot be negative.";

        return null;
    }

    private static AdviserProfileResponseV1 ToDto(Entities.Adviser adviser)
        => new(
            adviser.AdviserId,
            adviser.DisplayName,
            Normalize(adviser.MailboxUserId),
            Normalize(adviser.HomePostcode),
            Normalize(adviser.Region),
            adviser.BaseOfficeId,
            adviser.TeamName,
            adviser.ManagerId,
            adviser.Skills.ToArray(),
            adviser.Rating,
            adviser.IsActive,
            adviser.IsBookable,
            adviser.CoverageRadiusMiles,
            adviser.MaxTravelTimeMinutes,
            adviser.LastSyncedUtc ?? DateTime.MinValue);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Get(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key)
        => query.TryGetValue(key, out var value) ? value.FirstOrDefault() : null;

    private static bool GetBool(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key)
        => query.TryGetValue(key, out var values)
            && bool.TryParse(values.FirstOrDefault(), out var parsed)
            && parsed;
}
