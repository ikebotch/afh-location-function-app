using AFH.Location.Application.Abstractions;
using AFH.Location.Application.Models.V1;
using AFH.Location.Application.Services.Common;
using AFH.Location.Domain;
using Microsoft.Extensions.Logging;

namespace AFH.Location.Application.Services.V1;

public sealed class LocationSearchService : ILocationSearchService
{
    private const int MaxParallelOriginResolutions = 8;
    private readonly AdviserCandidateSource _candidateSource;
    private readonly ICalendarAvailabilityService _calendar;
    private readonly LocationResponseCandidateBuilder _responseCandidateBuilder;
    private readonly LocationSearchAuditWriter _searchAuditWriter;
    private readonly LocationSearchRoutingCoordinator _routingCoordinator;

    private readonly DestinationCoordinateResolver _destinationCoords;
    private readonly AdviserCoordinateResolver _adviserCoords;

    private readonly ICoveragePolicyProvider _coveragePolicyProvider;
    private readonly RouteMatrixCoordinator _matrixCoordinator;

    private readonly OfficeCoordinateResolver _officeCoords;
    private readonly IBaseOfficePolicyProvider _baseOfficePolicyProvider;
    private readonly IAvailabilityPolicyProvider _availabilityPolicyProvider;

    private readonly IRankingPolicyProvider _rankingPolicyProvider;
    private readonly RankingService _ranking;

    private readonly ILogger<LocationSearchService> _logger;

    public LocationSearchService(
        AdviserCandidateSource candidateSource,
        ICalendarAvailabilityService calendar,
        LocationResponseCandidateBuilder responseCandidateBuilder,
        LocationSearchAuditWriter searchAuditWriter,
        LocationSearchRoutingCoordinator routingCoordinator,
        DestinationCoordinateResolver destinationCoords,
        AdviserCoordinateResolver adviserCoords,
        ICoveragePolicyProvider coveragePolicyProvider,
        RouteMatrixCoordinator matrixCoordinator,
        OfficeCoordinateResolver officeCoords,
        IBaseOfficePolicyProvider baseOfficePolicyProvider,
        IAvailabilityPolicyProvider availabilityPolicyProvider,
        IRankingPolicyProvider rankingPolicyProvider,
        RankingService ranking,
        ILogger<LocationSearchService> logger)
    {
        _candidateSource = candidateSource;
        _calendar = calendar;
        _responseCandidateBuilder = responseCandidateBuilder;
        _searchAuditWriter = searchAuditWriter;
        _routingCoordinator = routingCoordinator;
        _destinationCoords = destinationCoords;
        _adviserCoords = adviserCoords;
        _coveragePolicyProvider = coveragePolicyProvider;
        _matrixCoordinator = matrixCoordinator;
        _officeCoords = officeCoords;
        _baseOfficePolicyProvider = baseOfficePolicyProvider;
        _availabilityPolicyProvider = availabilityPolicyProvider;
        _rankingPolicyProvider = rankingPolicyProvider;
        _ranking = ranking;
        _logger = logger;
    }

    public async Task<LocationSearchResult> SearchInPersonAsync(LocationSearchRequest req, CancellationToken ct)
    {
        var response = new LocationSearchResult
        {
            RequestId = req.RequestId,
            GeneratedAtUtc = DateTime.UtcNow
        };

        var ctx = new LocationSearchContext
        {
            Request = req,
            Response = response,
            DestResolved = default!, // set by ResolveDestinationAsync
            Destination = default!
        };

        await ResolveDestinationAsync(ctx, ct);
        await LoadPoliciesAsync(ctx, ct);
        await SourceCandidatesAsync(ctx, ct);
        if (ctx.Candidates.Count == 0) return response;

        await LoadAvailabilityAsync(ctx, ct);

        await ResolveAdviserOriginsAsync(ctx, ct);
        await ComputeMatrixRoutesToClientAsync(ctx, ct);

        await LoadOfficeDataAsync(ctx, ct);
        await PrecomputeTravelToBaseRoutesAsync(ctx, ct);
        await _routingCoordinator.ComputeNearestOfficeRouteAsync(ctx, ct);

        await _responseCandidateBuilder.BuildAsync(ctx, ct);
        RankAndApplyCandidates(ctx);
        await _searchAuditWriter.WriteAsync(ctx, ct);

        return response;
    }

    // ----------------------------
    // Steps
    // ----------------------------

    private async Task ResolveDestinationAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        ctx.DestResolved = await _destinationCoords.ResolveAsync(ctx.Request.Destination, ct);
        ctx.Destination = (ctx.DestResolved.Lat, ctx.DestResolved.Lng);

        if (ctx.DestResolved.Source == DestinationSource.GeocodedAddress)
        {
            ctx.Response.Warnings.Add(new LocationSearchWarning
            {
                Code = "DESTINATION_GEOCODED",
                Message = "Destination coordinates were derived from the supplied address."
            });
        }
    }

    private async Task SourceCandidatesAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        ctx.Candidates = await _candidateSource.GetCandidatesAsync(ctx.Request, ct);
        ApplyCandidateEligibilityFilters(ctx);

        if (ctx.Candidates.Count == 0)
        {
            ctx.Response.Warnings.Add(new LocationSearchWarning
            {
                Code = "NO_CANDIDATES",
                Message = "No advisers matched the supplied filters."
            });
        }
    }

    private async Task LoadAvailabilityAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        var extensionMinutes = GetAvailabilitySearchExtensionMinutes(ctx);
        var meetingWindow = new LocationMeetingWindow
        {
            RequestedStartUtc = ctx.Request.Meeting.RequestedStartUtc,
            DurationMinutes = ctx.Request.Meeting.DurationMinutes,
            SearchHorizonMinutes = Math.Max(1, ctx.Request.Meeting.SearchHorizonMinutes + extensionMinutes)
        };

        var mailboxByAdviserId = ctx.Candidates
            .Where(x => !string.IsNullOrWhiteSpace(x.Adviser.AdviserId))
            .GroupBy(x => x.Adviser.AdviserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var mailbox = g
                        .Select(x => x.Adviser.MailboxUserId)
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    return string.IsNullOrWhiteSpace(mailbox) ? g.Key : mailbox.Trim();
                },
                StringComparer.OrdinalIgnoreCase);

        var availability = await _calendar.GetAvailabilityAsync(
            mailboxByAdviserId.Values
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            meetingWindow,
            ct);

        var availabilityByMailbox = availability.ToDictionary(x => x.AdviserId, StringComparer.OrdinalIgnoreCase);
        ctx.AvailabilityById = mailboxByAdviserId
            .Where(x => availabilityByMailbox.ContainsKey(x.Value))
            .ToDictionary(
                x => x.Key,
                x => availabilityByMailbox[x.Value],
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task LoadPoliciesAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        // Keep these sequential because SQL-backed policy providers share the same scoped DbContext.
        // Parallel queries on one DbContext trigger EF Core concurrency exceptions.
        ctx.CoveragePolicy = await _coveragePolicyProvider.GetAsync(ct);
        ctx.BaseOfficePolicy = await _baseOfficePolicyProvider.GetAsync(ct);
        ctx.RankingPolicy = await _rankingPolicyProvider.GetAsync(ct);
        ctx.AvailabilityPolicy = await _availabilityPolicyProvider.GetAsync(ct);
    }

    private async Task ResolveAdviserOriginsAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        var (destLat, destLng) = ctx.Destination;
        var requestedStartUtc = ctx.Request.Meeting.RequestedStartUtc;
        using var gate = new SemaphoreSlim(MaxParallelOriginResolutions, MaxParallelOriginResolutions);
        var sync = new object();

        var tasks = ctx.Candidates.Select(async c =>
        {
            await gate.WaitAsync(ct);
            try
            {
                ctx.AvailabilityById.TryGetValue(c.Adviser.AdviserId, out var availability);

                if (ctx.AvailabilityPolicy.RequireCalendarAvailability &&
                    (availability is null || availability.State != CalendarAvailabilityState.Ok))
                {
                    lock (sync)
                        ctx.OriginSourceById[c.Adviser.AdviserId] = "SKIPPED_UNAVAILABLE";
                    return;
                }

                var (originPostcode, source, gapFromPreviousMinutes) = SelectOriginPostcode(ctx, availability, requestedStartUtc);
                var origin = await _adviserCoords.ResolveHomeAsync(c.Adviser, ct, originPostcode);
                if (IsZero(origin))
                    return;

                var air = CoverageEvaluator.HaversineMiles(origin.Lat, origin.Lng, destLat, destLng);

                lock (sync)
                {
                    ctx.AdviserOrigins[c.Adviser.AdviserId] = origin;
                    ctx.OriginSourceById[c.Adviser.AdviserId] = source;
                    ctx.AirMilesById[c.Adviser.AdviserId] = air;
                }

                if (gapFromPreviousMinutes.HasValue)
                    _logger.LogDebug(
                        "Origin selected from previous client for AdviserId={AdviserId}. GapMinutes={GapMinutes}",
                        c.Adviser.AdviserId,
                        gapFromPreviousMinutes.Value);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task ComputeMatrixRoutesToClientAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        var (destLat, destLng) = ctx.Destination;

        var withinCoverage = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in ctx.Candidates)
        {
            if (!ctx.AdviserOrigins.TryGetValue(c.Adviser.AdviserId, out var origin))
                continue;

            if (!IsCalendarRoutingEligible(ctx, c.Adviser.AdviserId))
                continue;

            if (IsWithinCoverage(ctx, c.Adviser.AdviserId, c.Adviser.Region))
                withinCoverage[c.Adviser.AdviserId] = origin;
        }

        if (withinCoverage.Count == 0)
        {
            ctx.RoutesToClient = new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        ctx.RoutesToClient = await _matrixCoordinator.GetRoutesAsync(withinCoverage, (destLat, destLng), ct);
    }

    private async Task LoadOfficeDataAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        ctx.OfficeCoords = await _officeCoords.GetOfficeCoordsAsync(ct);

        var (destLat, destLng) = ctx.Destination;
        ctx.NearestOfficeId = FindNearestOfficeId(destLat, destLng, ctx.OfficeCoords);
    }

    private async Task PrecomputeTravelToBaseRoutesAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        var adviserRouteKeyById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var uniqueHomeDestinations = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in ctx.Candidates)
        {
            if (!ctx.AdviserOrigins.TryGetValue(candidate.Adviser.AdviserId, out var origin))
                continue;

            var routeKey = BuildCoordinateLookupKey(origin);
            adviserRouteKeyById[candidate.Adviser.AdviserId] = routeKey;
            uniqueHomeDestinations.TryAdd(routeKey, origin);
        }

        var homeRoutesByKey = await _matrixCoordinator.GetRoutesFromOriginAsync(ctx.Destination, uniqueHomeDestinations, ct);
        ctx.RoutesToHomeByAdviserId = adviserRouteKeyById
            .Where(x => homeRoutesByKey.ContainsKey(x.Value))
            .ToDictionary(
                x => x.Key,
                x => homeRoutesByKey[x.Value],
                StringComparer.OrdinalIgnoreCase);

        var officeDestinations = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in ctx.Candidates)
        {
            var officeId = ResolveBaseOfficeId(candidate.Adviser.Region, ctx.BaseOfficePolicy);
            if (string.IsNullOrWhiteSpace(officeId))
                continue;

            if (ctx.OfficeCoords.TryGetValue(officeId, out var officeCoords) && !IsZero(officeCoords))
                officeDestinations.TryAdd(officeId, officeCoords);
        }

        if (!string.IsNullOrWhiteSpace(ctx.NearestOfficeId) &&
            ctx.OfficeCoords.TryGetValue(ctx.NearestOfficeId, out var nearestOfficeCoords) &&
            !IsZero(nearestOfficeCoords))
        {
            officeDestinations.TryAdd(ctx.NearestOfficeId, nearestOfficeCoords);
        }

        ctx.RoutesToOfficeByOfficeId = await _matrixCoordinator.GetRoutesFromOriginAsync(ctx.Destination, officeDestinations, ct);

        if (!string.IsNullOrWhiteSpace(ctx.NearestOfficeId) &&
            ctx.RoutesToOfficeByOfficeId.TryGetValue(ctx.NearestOfficeId, out var nearestOfficeRoute) &&
            nearestOfficeRoute.EtaMinutes > 0)
        {
            ctx.NearestOfficeRoute = nearestOfficeRoute;
        }
    }

    private void RankAndApplyCandidates(LocationSearchContext ctx)
    {
        var ranked = ctx.Response.Candidates
            .Select(c => _ranking.Rank(c, ctx.RankingPolicy))
            .OrderBy(x => x.Score)
            .ThenByDescending(x => x.Candidate.AdviserRating)
            .ToList();

        // apply ranks
        ctx.Response.Candidates.Clear();
        var rank = 1;
        foreach (var r in ranked)
        {
            r.Candidate.Rank = rank++;
            r.Candidate.Score = r.Score;
            foreach (var rr in r.RankingReasons)
                r.Candidate.Reasons.Add(rr);

            ctx.Response.Candidates.Add(r.Candidate);
        }

        var maxScore = ctx.Request.Filters?.MaxRankingScore ?? ctx.RankingPolicy.MaxEligibleScore;
        if (maxScore is not null)
            ctx.Response.Candidates = ctx.Response.Candidates.Where(c => c.Score <= maxScore.Value).ToList();

        var max = ctx.Request.Filters?.MaxCandidates;
        if (max is int n && n > 0)
            ctx.Response.Candidates = ctx.Response.Candidates.Take(n).ToList();
    }

    // ----------------------------
    // Helpers (small + readable)
    // ----------------------------

    private static bool IsZero((double Lat, double Lng) x) => x.Lat == 0d && x.Lng == 0d;

    private static string BuildCoordinateLookupKey((double Lat, double Lng) coordinates)
        => $"{coordinates.Lat:F6}:{coordinates.Lng:F6}";

    private static int GetAvailabilitySearchExtensionMinutes(LocationSearchContext ctx)
    {
        var requestedTravelBuffer = ctx.Request.Filters?.BufferMinutes;
        var effectiveTravelBuffer = requestedTravelBuffer ?? ctx.AvailabilityPolicy.DefaultBufferMinutes;
        var travel = Math.Clamp(effectiveTravelBuffer, 0, Math.Max(0, ctx.AvailabilityPolicy.MaxBufferMinutes));

        const int fallbackDefaultCompanyMinutes = 30;
        var requestedCompanyBuffer = ctx.Request.Filters?.CompanyBufferMinutes;
        var policyDefaultCompanyBuffer = ctx.AvailabilityPolicy.DefaultCompanyBufferMinutes > 0
            ? ctx.AvailabilityPolicy.DefaultCompanyBufferMinutes
            : fallbackDefaultCompanyMinutes;
        var effectiveCompanyBuffer = requestedCompanyBuffer ?? policyDefaultCompanyBuffer;
        var company = Math.Clamp(effectiveCompanyBuffer, 0, Math.Max(0, ctx.AvailabilityPolicy.MaxCompanyBufferMinutes));

        return Math.Max(0, travel) + Math.Max(0, company);
    }

    private static bool IsWithinCoverage(LocationSearchContext ctx, string adviserId, string region)
    {
        var radius = GetRadiusMiles(ctx, adviserId, region);
        return ctx.AirMilesById.TryGetValue(adviserId, out var miles) && miles <= radius;
    }

    private static double GetRadiusMiles(LocationSearchContext ctx, string adviserId, string region)
    {
        if (ctx.CoveragePolicy.AdviserRadiusMiles.TryGetValue(adviserId, out var adviserRadius))
            return adviserRadius;

        if (ctx.CoveragePolicy.RegionRadiusMiles.TryGetValue(region, out var regionRadius))
            return regionRadius;

        return ctx.CoveragePolicy.DefaultRadiusMiles;
    }

    private static void ApplyCandidateEligibilityFilters(LocationSearchContext ctx)
    {
        var minRating = ctx.Request.Filters?.MinAdviserRating ?? ctx.RankingPolicy.DefaultMinAdviserRating;
        if (minRating <= 0) return;

        ctx.Candidates = ctx.Candidates
            .Where(c => c.Adviser.Rating >= minRating)
            .ToList();
    }

    private static bool IsCalendarRoutingEligible(LocationSearchContext ctx, string adviserId)
    {
        if (!ctx.AvailabilityPolicy.RequireCalendarAvailability)
            return true;

        if (!ctx.AvailabilityById.TryGetValue(adviserId, out var availability))
            return false;

        return availability.State == CalendarAvailabilityState.Ok;
    }

    private static string ResolveBaseOfficeId(string region, BaseOfficePolicy policy)
    {
        if (policy.RegionOfficeMap.TryGetValue(region, out var officeId) && !string.IsNullOrWhiteSpace(officeId))
            return officeId;

        return policy.DefaultOfficeId;
    }

    private static (string? OriginPostcode, string Source, int? GapFromPreviousMinutes) SelectOriginPostcode(
        LocationSearchContext ctx,
        AdviserAvailability? availability,
        DateTime requestedStartUtc)
    {
        if (availability is null || availability.BusyBlocks.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(availability?.CurrentLocationPostcode))
                return (availability.CurrentLocationPostcode, "CURRENT_LOCATION", null);

            return (null, "HOME", null);
        }

        var previous = availability.BusyBlocks
            .Where(x => x.EndUtc <= requestedStartUtc && !string.IsNullOrWhiteSpace(x.LocationPostcode))
            .OrderByDescending(x => x.EndUtc)
            .FirstOrDefault();

        if (previous is not null)
        {
            var gap = (int)Math.Max(0, (requestedStartUtc - previous.EndUtc).TotalMinutes);
            if (gap <= ctx.AvailabilityPolicy.PreviousClientProximityMinutes)
                return (previous.LocationPostcode, "PREVIOUS_CLIENT", gap);
        }

        if (!string.IsNullOrWhiteSpace(availability.CurrentLocationPostcode))
            return (availability.CurrentLocationPostcode, "CURRENT_LOCATION", null);

        return (null, "HOME", null);
    }

    private static string? FindNearestOfficeId(
        double destLat,
        double destLng,
        IReadOnlyDictionary<string, (double Lat, double Lng)> officeCoords)
    {
        string? nearestId = null;
        var best = double.MaxValue;

        foreach (var kv in officeCoords)
        {
            var (olat, olng) = kv.Value;
            if (olat == 0d && olng == 0d) continue;

            var air = CoverageEvaluator.HaversineMiles(destLat, destLng, olat, olng);
            if (air < best)
            {
                best = air;
                nearestId = kv.Key;
            }
        }

        return nearestId;
    }
}
