using System.Net;
using AFH.Adviser.Application.Abstractions.Availability;
using AFH.Adviser.Application.Models.Availability;
using AFH.Adviser.Contract.V1.Availability;
using AFH.Location.Function.Docs.V1;
using AFH.Location.Function.Functions.Common;
using AFH.Location.Function.Mapping.V1.Adviser;
using AFH.Location.Function.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Location.Function.Functions.V1.Adviser;

public sealed class AdviserAvailabilityRulesFunctionV1
{
    private readonly IAdviserAvailabilityRulesService _rules;
    private readonly IDomainUserAuthorizationService _auth;

    public AdviserAvailabilityRulesFunctionV1(
        IAdviserAvailabilityRulesService rules,
        IDomainUserAuthorizationService auth)
    {
        _rules = rules;
        _auth = auth;
    }

    [Function("AdviserAvailabilityRulesActiveV1")]
    [LocationOpenApiOperation("Admin", "Get active adviser availability rules",
        Description = "Returns the active adviser-owned working pattern and capacity rules for a consumer context such as Booking.",
        ResponseType = typeof(AdviserAvailabilityRulesResponseV1))]
    [LocationOpenApiQueryParameter("projectContext", "string", Description = "Consumer context. Defaults to Booking.")]
    public async Task<HttpResponseData> GetActiveAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/advisers/availability-rules/active")]
        HttpRequestData req,
        CancellationToken ct)
        => await WriteActiveRulesAsync(req, null, requireActiveRules: true, ct);

    [Function("AdviserAvailabilityRulesListV1")]
    [LocationOpenApiOperation("Admin", "Get adviser availability rules",
        Description = "Returns active adviser-owned working pattern and capacity rules. Supports adviserId and projectContext filters.",
        ResponseType = typeof(AdviserAvailabilityRulesResponseV1))]
    [LocationOpenApiQueryParameter("adviserId", "string")]
    [LocationOpenApiQueryParameter("projectContext", "string", Description = "Consumer context. Defaults to Booking.")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/availability-rules")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var query = QueryHelpers.ParseQuery(req.Url.Query);
        return await WriteActiveRulesAsync(req, Get(query, "adviserId"), requireActiveRules: false, ct);
    }

    [Function("AdviserAvailabilityRulesForAdviserV1")]
    [LocationOpenApiOperation("Admin", "Get availability rules for adviser",
        Description = "Returns active working pattern and capacity rules for a single adviser and project context.",
        ResponseType = typeof(AdviserAvailabilityRulesResponseV1))]
    [LocationOpenApiQueryParameter("projectContext", "string", Description = "Consumer context. Defaults to Booking.")]
    public async Task<HttpResponseData> GetForAdviserAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/advisers/{adviserId}/availability-rules")]
        HttpRequestData req,
        string adviserId,
        CancellationToken ct)
        => await WriteActiveRulesAsync(req, adviserId, requireActiveRules: false, ct);

    [Function("AdviserAvailabilityRulesCreateV1")]
    [LocationOpenApiOperation("Admin", "Create adviser availability rule",
        Description = "Creates an adviser working-pattern rule in the active availability ruleset.",
        ResponseType = typeof(AvailabilityRuleResponseV1))]
    public async Task<HttpResponseData> CreateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/availability-rules")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, "Calendar.Manage", allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<AvailabilityRuleUpsertRequestV1>(ct);
        if (body is null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_AVAILABILITY_RULE", message = "Request body is required." }, ct);

        try
        {
            var created = await _rules.CreateRuleAsync(ToUpsert(body), ct);
            return await req.WriteSuccessAsync(ToRuleResponse(created), ct, statusCode: HttpStatusCode.Created);
        }
        catch (ArgumentException ex)
        {
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_AVAILABILITY_RULE", message = ex.Message }, ct);
        }
    }

    [Function("AdviserAvailabilityRulesUpdateV1")]
    [LocationOpenApiOperation("Admin", "Update adviser availability rule",
        Description = "Updates an adviser working-pattern rule in the active availability ruleset.",
        ResponseType = typeof(AvailabilityRuleResponseV1))]
    public async Task<HttpResponseData> UpdateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/availability-rules/{id}")]
        HttpRequestData req,
        string id,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, "Calendar.Manage", allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<AvailabilityRuleUpsertRequestV1>(ct);
        if (body is null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_AVAILABILITY_RULE", message = "Request body is required." }, ct);

        try
        {
            var updated = await _rules.UpdateRuleAsync(id, ToUpsert(body), ct);
            return updated is null
                ? await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "AVAILABILITY_RULE_NOT_FOUND", message = "Availability rule was not found." }, ct)
                : await req.WriteSuccessAsync(ToRuleResponse(updated), ct);
        }
        catch (ArgumentException ex)
        {
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_AVAILABILITY_RULE", message = ex.Message }, ct);
        }
    }

    [Function("AdviserAvailabilityRulesDeleteV1")]
    [LocationOpenApiOperation("Admin", "Delete adviser availability rule",
        Description = "Disables an adviser working-pattern rule in the active availability ruleset.")]
    public async Task<HttpResponseData> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/availability-rules/{id}")]
        HttpRequestData req,
        string id,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, "Calendar.Manage", allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var deleted = await _rules.DeleteRuleAsync(id, ct);
        return deleted
            ? await req.WriteSuccessAsync(new { id, deleted = true }, ct)
            : await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "AVAILABILITY_RULE_NOT_FOUND", message = "Availability rule was not found." }, ct);
    }

    [Function("AdviserAvailabilityTimeSlotsV1")]
    [LocationOpenApiOperation("Admin", "Get adviser availability time slots",
        Description = "Returns generated working-pattern slots for availability diagnostics. Calendar busy/free remains owned by Calendar Service.",
        ResponseType = typeof(AvailabilityTimeSlotsResponseV1))]
    [LocationOpenApiQueryParameter("adviserId", "string")]
    [LocationOpenApiQueryParameter("from", "date")]
    [LocationOpenApiQueryParameter("to", "date")]
    [LocationOpenApiQueryParameter("projectContext", "string", Description = "Consumer context. Defaults to Booking.")]
    public async Task<HttpResponseData> GetTimeSlotsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/availability/time-slots")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, "Calendar.Read", allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var query = QueryHelpers.ParseQuery(req.Url.Query);
        var result = await _rules.GetAdminTimeSlotsAsync(new AvailabilityTimeSlotsQuery
        {
            ProjectContext = Get(query, "projectContext"),
            AdviserId = Get(query, "adviserId"),
            From = GetDate(query, "from"),
            To = GetDate(query, "to")
        }, ct);

        if (!result.Succeeded)
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.BadRequest,
                new { code = result.ErrorCode, message = result.ErrorMessage },
                ct);
        }

        var response = new AvailabilityTimeSlotsResponseV1(MapSlots(result.Slots));
        return await req.WriteSuccessAsync(response, ct, ApiEnvelopeExtensions.SinglePage(response.Slots.Count));
    }

    [Function("AdviserAvailabilityTimeSlotOverrideCreateV1")]
    [LocationOpenApiOperation("Admin", "Create adviser time slot override",
        Description = "Creates a one-off adviser availability slot override by writing a date-scoped availability rule.",
        ResponseType = typeof(AvailabilityTimeSlotOverrideResponseV1))]
    public async Task<HttpResponseData> CreateTimeSlotOverrideAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/availability/time-slots/overrides")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, "Calendar.Slots.Override", allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<AvailabilityTimeSlotOverrideRequestV1>(ct);
        if (body is null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_TIME_SLOT_OVERRIDE", message = "Request body is required." }, ct);

        try
        {
            var created = await _rules.CreateRuleAsync(ToOverrideUpsert(body), ct);
            return await req.WriteSuccessAsync(ToOverrideResponse(created, body), ct, statusCode: HttpStatusCode.Created);
        }
        catch (ArgumentException ex)
        {
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_TIME_SLOT_OVERRIDE", message = ex.Message }, ct);
        }
    }

    [Function("AdviserAvailabilityTimeSlotOverrideUpdateV1")]
    [LocationOpenApiOperation("Admin", "Update adviser time slot override",
        Description = "Updates a one-off adviser availability slot override.",
        ResponseType = typeof(AvailabilityTimeSlotOverrideResponseV1))]
    public async Task<HttpResponseData> UpdateTimeSlotOverrideAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/availability/time-slots/overrides/{id}")]
        HttpRequestData req,
        string id,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, "Calendar.Slots.Override", allowInternal: false, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<AvailabilityTimeSlotOverrideRequestV1>(ct);
        if (body is null)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_TIME_SLOT_OVERRIDE", message = "Request body is required." }, ct);

        try
        {
            var updated = await _rules.UpdateRuleAsync(id, ToOverrideUpsert(body), ct);
            return updated is null
                ? await req.WriteFailureAsync(HttpStatusCode.NotFound, new { code = "TIME_SLOT_OVERRIDE_NOT_FOUND", message = "Time slot override was not found." }, ct)
                : await req.WriteSuccessAsync(ToOverrideResponse(updated, body), ct);
        }
        catch (ArgumentException ex)
        {
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_TIME_SLOT_OVERRIDE", message = ex.Message }, ct);
        }
    }

    private async Task<HttpResponseData> WriteActiveRulesAsync(
        HttpRequestData req,
        string? adviserId,
        bool requireActiveRules,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, "Calendar.Read", allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var query = QueryHelpers.ParseQuery(req.Url.Query);
        var projectContext = query.TryGetValue("projectContext", out var values) ? values.FirstOrDefault() : null;

        if (requireActiveRules)
        {
            var activeRules = await _rules.GetActiveRulesAsync(projectContext, ct);
            if (activeRules is not null)
                return await req.WriteSuccessAsync(AdviserAvailabilityRulesContractMapper.ToContractResponse(activeRules), ct);

            return await req.WriteFailureAsync(
                HttpStatusCode.NotFound,
                new { code = "ADVISER_AVAILABILITY_RULES_NOT_FOUND", message = "No active adviser availability rule set was found." },
                ct);
        }

        var rules = await _rules.GetAdminRulesAsync(projectContext, adviserId, ct);
        return await req.WriteSuccessAsync(AdviserAvailabilityRulesContractMapper.ToContractResponse(rules), ct);
    }

    private static IReadOnlyList<AvailabilityTimeSlotResponseV1> MapSlots(IReadOnlyList<AvailabilityTimeSlot> slots)
        => slots
            .Select(slot => new AvailabilityTimeSlotResponseV1(
                slot.Id,
                slot.AdviserId,
                slot.Date,
                slot.StartTime,
                slot.EndTime,
                slot.IsBooked,
                slot.BookingId,
                slot.Status))
            .ToArray();

    private static string? Get(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key)
        => query.TryGetValue(key, out var value) ? value.FirstOrDefault() : null;

    private static DateOnly? GetDate(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key)
        => query.TryGetValue(key, out var values) && DateOnly.TryParse(values.FirstOrDefault(), out var parsed)
            ? parsed
            : null;

    private static AvailabilityRuleUpsert ToUpsert(AvailabilityRuleUpsertRequestV1 request)
        => new()
        {
            ProjectContext = request.ProjectContext,
            AdviserId = request.AdviserId ?? string.Empty,
            AdviserName = request.AdviserName,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime ?? string.Empty,
            EndTime = request.EndTime ?? string.Empty,
            Capacity = request.Capacity,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = request.Status,
            Notes = request.Notes
        };

    private static AvailabilityRuleUpsert ToOverrideUpsert(AvailabilityTimeSlotOverrideRequestV1 request)
    {
        if (string.IsNullOrWhiteSpace(request.Date) || !DateOnly.TryParse(request.Date, out var date))
            throw new ArgumentException("date must be a valid date.", nameof(request));

        var reason = request.Reason?.Trim();
        var overrideType = request.OverrideType?.Trim();
        var notes = string.IsNullOrWhiteSpace(overrideType)
            ? reason
            : string.IsNullOrWhiteSpace(reason) ? overrideType : $"{overrideType}: {reason}";

        return new AvailabilityRuleUpsert
        {
            ProjectContext = request.ProjectContext,
            AdviserId = request.AdviserId ?? string.Empty,
            AdviserName = request.AdviserName,
            DayOfWeek = date.DayOfWeek.ToString(),
            StartTime = request.StartTime ?? string.Empty,
            EndTime = request.EndTime ?? string.Empty,
            Capacity = request.Capacity ?? (request.IsBooked ? 0 : 1),
            EffectiveFrom = date.ToString("yyyy-MM-dd"),
            EffectiveTo = date.ToString("yyyy-MM-dd"),
            Status = request.Status,
            Notes = notes
        };
    }

    private static AvailabilityRuleResponseV1 ToRuleResponse(AvailabilityRuleRecord record)
        => new(
            record.Id,
            record.AdviserId,
            record.AdviserName,
            record.DayOfWeek,
            record.StartTime,
            record.EndTime,
            record.Capacity,
            record.EffectiveFrom,
            record.EffectiveTo,
            record.Status,
            record.Notes);

    private static AvailabilityTimeSlotOverrideResponseV1 ToOverrideResponse(
        AvailabilityRuleRecord record,
        AvailabilityTimeSlotOverrideRequestV1 request)
        => new(
            record.Id,
            record.AdviserId,
            record.AdviserName,
            record.EffectiveFrom ?? request.Date ?? string.Empty,
            record.StartTime,
            record.EndTime,
            request.IsBooked,
            null,
            request.Status ?? record.Status,
            request.OverrideType,
            request.Reason);
}
