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
        => await WriteActiveRulesAsync(req, null, returnNotFoundWhenMissing: true, ct);

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
        return await WriteActiveRulesAsync(req, Get(query, "adviserId"), returnNotFoundWhenMissing: false, ct);
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
        => await WriteActiveRulesAsync(req, adviserId, returnNotFoundWhenMissing: false, ct);

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
        var projectContext = Get(query, "projectContext");
        var adviserId = Get(query, "adviserId");

        var rules = await _rules.GetActiveRulesAsync(projectContext, ct);
        if (rules is null)
        {
            var emptyResponse = new AvailabilityTimeSlotsResponseV1([]);
            return await req.WriteSuccessAsync(emptyResponse, ct, ApiEnvelopeExtensions.SinglePage(0));
        }

        var from = GetDate(query, "from") ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var to = GetDate(query, "to") ?? from.AddDays(Math.Max(0, rules.CapacityWindowDays - 1));
        if (to < from)
            return await req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "INVALID_DATE_RANGE", message = "to must be on or after from." }, ct);

        var response = new AvailabilityTimeSlotsResponseV1(GenerateSlots(rules, adviserId, from, to));
        return await req.WriteSuccessAsync(response, ct, ApiEnvelopeExtensions.SinglePage(response.Slots.Count));
    }

    private async Task<HttpResponseData> WriteActiveRulesAsync(
        HttpRequestData req,
        string? adviserId,
        bool returnNotFoundWhenMissing,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, "Calendar.Read", allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var query = QueryHelpers.ParseQuery(req.Url.Query);
        var projectContext = query.TryGetValue("projectContext", out var values) ? values.FirstOrDefault() : null;

        var rules = await _rules.GetActiveRulesAsync(projectContext, ct);
        if (rules is null)
        {
            if (!returnNotFoundWhenMissing)
            {
                return await req.WriteSuccessAsync(
                    AdviserAvailabilityRulesContractMapper.ToContractResponse(new AdviserAvailabilityRules()),
                    ct);
            }

            return await req.WriteFailureAsync(
                HttpStatusCode.NotFound,
                new { code = "ADVISER_AVAILABILITY_RULES_NOT_FOUND", message = "No active adviser availability rule set was found." },
                ct);
        }

        var filtered = FilterRulesForAdviser(rules, adviserId);
        return await req.WriteSuccessAsync(AdviserAvailabilityRulesContractMapper.ToContractResponse(filtered), ct);
    }

    private static AdviserAvailabilityRules FilterRulesForAdviser(AdviserAvailabilityRules rules, string? adviserId)
    {
        if (string.IsNullOrWhiteSpace(adviserId))
            return rules;

        return new AdviserAvailabilityRules
        {
            MinimumAppointmentMinutes = rules.MinimumAppointmentMinutes,
            DefaultWorkingDayStart = rules.DefaultWorkingDayStart,
            DefaultWorkingDayEnd = rules.DefaultWorkingDayEnd,
            CapacityWindowDays = rules.CapacityWindowDays,
            WorkingPatterns = rules.WorkingPatterns
                .Where(rule => string.Equals(rule.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase))
                .ToArray(),
            CapacityLimits = rules.CapacityLimits
                .Where(rule => string.Equals(rule.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase))
                .ToArray()
        };
    }

    private static IReadOnlyList<AvailabilityTimeSlotResponseV1> GenerateSlots(
        AdviserAvailabilityRules rules,
        string? adviserId,
        DateOnly from,
        DateOnly to)
    {
        var filtered = FilterRulesForAdviser(rules, adviserId);
        if (filtered.WorkingPatterns.Count == 0)
            return [];

        var duration = Math.Max(1, filtered.MinimumAppointmentMinutes);
        var slots = new List<AvailabilityTimeSlotResponseV1>();

        foreach (var pattern in filtered.WorkingPatterns)
        {
            if (!TimeOnly.TryParse(pattern.Start, out var start) || !TimeOnly.TryParse(pattern.End, out var end) || end <= start)
                continue;

            for (var date = from; date <= to; date = date.AddDays(1))
            {
                for (var slotStart = start; slotStart.AddMinutes(duration) <= end; slotStart = slotStart.AddMinutes(duration))
                {
                    var slotEnd = slotStart.AddMinutes(duration);
                    slots.Add(new AvailabilityTimeSlotResponseV1(
                        $"{pattern.AdviserId}:{date:yyyyMMdd}:{slotStart:HHmm}",
                        pattern.AdviserId,
                        date.ToString("yyyy-MM-dd"),
                        slotStart.ToString("HH:mm"),
                        slotEnd.ToString("HH:mm"),
                        false,
                        null,
                        "Available"));
                }
            }
        }

        return slots
            .OrderBy(slot => slot.Date, StringComparer.Ordinal)
            .ThenBy(slot => slot.StartTime, StringComparer.Ordinal)
            .ThenBy(slot => slot.AdviserId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? Get(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key)
        => query.TryGetValue(key, out var value) ? value.FirstOrDefault() : null;

    private static DateOnly? GetDate(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key)
        => query.TryGetValue(key, out var values) && DateOnly.TryParse(values.FirstOrDefault(), out var parsed)
            ? parsed
            : null;
}

public sealed record AvailabilityTimeSlotsResponseV1(IReadOnlyList<AvailabilityTimeSlotResponseV1> Slots);

public sealed record AvailabilityTimeSlotResponseV1(
    string Id,
    string AdviserId,
    string Date,
    string StartTime,
    string EndTime,
    bool IsBooked,
    string? BookingId,
    string Status);
