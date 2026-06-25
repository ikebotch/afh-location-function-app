using System.Net;
using AFH.Adviser.Application.Abstractions.Skills;
using AFH.Adviser.Application.Models.Auth;
using AFH.Adviser.Application.Models.Skills;
using AFH.Adviser.Contract.V1.Skills;
using AFH.Location.Function.Docs.V1;
using AFH.Location.Function.Functions.Common;
using AFH.Location.Function.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Function.Functions.V1.Adviser;

public sealed class AdviserSpecialismsFunctionV1
{
    private readonly IAdviserSkillAdminService _skills;
    private readonly IDomainUserAuthorizationService _auth;

    public AdviserSpecialismsFunctionV1(IAdviserSkillAdminService skills, IDomainUserAuthorizationService auth)
    {
        _skills = skills;
        _auth = auth;
    }

    [Function("AdviserSpecialismsListV1")]
    [LocationOpenApiOperation("Admin", "List adviser specialisms",
        Description = "Returns the managed adviser skill and licence catalog.",
        ResponseType = typeof(AdviserSpecialismsResponseV1))]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/adviser-specialisms")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, AdviserAdminPermissions.SkillsRead, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var query = QueryHelpers.ParseQuery(req.Url.Query);
        var includeInactive = query.TryGetValue("includeInactive", out var values)
            && bool.TryParse(values.FirstOrDefault(), out var parsed)
            && parsed;
        var skills = await _skills.ListAsync(includeInactive, ct);
        var response = new AdviserSpecialismsResponseV1(skills.Select(ToDto).ToArray());
        return await req.WriteSuccessAsync(response, ct, ApiEnvelopeExtensions.SinglePage(response.Specialisms.Count));
    }

    [Function("AdviserSpecialismsCreateV1")]
    public async Task<HttpResponseData> CreateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/adviser-specialisms")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, AdviserAdminPermissions.SkillsManage, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<AdviserSpecialismUpsertRequestV1>(ct);
        var validation = Validate(body);
        if (validation is not null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_ADVISER_SPECIALISM", message = validation }, ct);

        var created = await _skills.UpsertAsync(null, ToUpsert(body!), ct);
        return await req.WriteSuccessAsync(ToDto(created), ct, statusCode: HttpStatusCode.Created);
    }

    [Function("AdviserSpecialismsUpdateV1")]
    public async Task<HttpResponseData> UpdateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/adviser-specialisms/{specialismId:guid}")]
        HttpRequestData req,
        Guid specialismId,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, AdviserAdminPermissions.SkillsManage, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<AdviserSpecialismUpsertRequestV1>(ct);
        var validation = Validate(body);
        if (validation is not null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_ADVISER_SPECIALISM", message = validation }, ct);

        var updated = await _skills.UpsertAsync(specialismId, ToUpsert(body!), ct);
        return await req.WriteSuccessAsync(ToDto(updated), ct);
    }

    [Function("AdviserSpecialismsDeleteV1")]
    public async Task<HttpResponseData> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/adviser-specialisms/{specialismId:guid}")]
        HttpRequestData req,
        Guid specialismId,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, AdviserAdminPermissions.SkillsManage, allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var deleted = await _skills.DeleteAsync(specialismId, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { specialismId, deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "ADVISER_SPECIALISM_NOT_FOUND", message = "Adviser specialism was not found." }, ct);
    }

    private static string? Validate(AdviserSpecialismUpsertRequestV1? body)
    {
        if (body is null)
            return "Request body is required.";
        if (string.IsNullOrWhiteSpace(body.Name))
            return "Specialism name is required.";
        if (body.RenewalMonths is < 0)
            return "Renewal months cannot be negative.";

        return null;
    }

    private static AdviserSkillUpsert ToUpsert(AdviserSpecialismUpsertRequestV1 body)
        => new(
            body.Name!.Trim(),
            Normalize(body.Category),
            Normalize(body.Description),
            body.LicenseRequired.GetValueOrDefault(),
            Normalize(body.Certification),
            body.RenewalMonths,
            body.IsActive.GetValueOrDefault(true));

    private static AdviserSpecialismResponseV1 ToDto(AdviserSkillCatalogItem item)
        => new(
            item.Id,
            item.Name,
            item.Category,
            item.Description,
            item.LicenseRequired,
            item.Certification,
            item.RenewalMonths,
            item.IsActive);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
