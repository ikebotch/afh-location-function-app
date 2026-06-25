using System.Net;
using AFH.Adviser.Application.Abstractions.Availability;
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
    {
        var authFailure = await _auth.AuthorizeAsync(req, "Calendar.Read", allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var query = QueryHelpers.ParseQuery(req.Url.Query);
        var projectContext = query.TryGetValue("projectContext", out var values) ? values.FirstOrDefault() : null;

        var rules = await _rules.GetActiveRulesAsync(projectContext, ct);
        if (rules is null)
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.NotFound,
                new { code = "ADVISER_AVAILABILITY_RULES_NOT_FOUND", message = "No active adviser availability rule set was found." },
                ct);
        }

        return await req.WriteSuccessAsync(AdviserAvailabilityRulesContractMapper.ToContractResponse(rules), ct);
    }
}

