using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Contracts.V1.Requests;
using AFH.Location.Service.Core.Contracts.V1.Responses;
using AFH.Location.Service.Core.Services.Common;
using Microsoft.Extensions.Logging;

namespace AFH.Location.Service.Core.Services.V1;

public sealed class LocationSearchServiceV1 : ILocationSearchService
{
    private readonly AdviserCandidateSource _candidateSource;
    private readonly ICalendarAvailabilityService _calendar;

    private readonly DestinationCoordinateResolver _destinationCoords;
    private readonly AdviserCoordinateResolver _adviserCoords;

    private readonly ICoveragePolicyProvider _coveragePolicyProvider;
    private readonly RouteMatrixCoordinator _matrixCoordinator;

    private readonly OfficeCoordinateResolver _officeCoords;
    private readonly IBaseOfficePolicyProvider _baseOfficePolicyProvider;
    private readonly IAvailabilityPolicyProvider _availabilityPolicyProvider;

    private readonly IRoutingService _routing;
    private readonly IRankingPolicyProvider _rankingPolicyProvider;
    private readonly RankingService _ranking;

    private readonly ILogger<LocationSearchServiceV1> _logger;

    public LocationSearchServiceV1(
        AdviserCandidateSource candidateSource,
        ICalendarAvailabilityService calendar,
        DestinationCoordinateResolver destinationCoords,
        AdviserCoordinateResolver adviserCoords,
        ICoveragePolicyProvider coveragePolicyProvider,
        RouteMatrixCoordinator matrixCoordinator,
        OfficeCoordinateResolver officeCoords,
        IBaseOfficePolicyProvider baseOfficePolicyProvider,
        IAvailabilityPolicyProvider availabilityPolicyProvider,
        IRoutingService routing,
        IRankingPolicyProvider rankingPolicyProvider,
        RankingService ranking,
        ILogger<LocationSearchServiceV1> logger)
    {
        _candidateSource = candidateSource;
        _calendar = calendar;
        _destinationCoords = destinationCoords;
        _adviserCoords = adviserCoords;
        _coveragePolicyProvider = coveragePolicyProvider;
        _matrixCoordinator = matrixCoordinator;
        _officeCoords = officeCoords;
        _baseOfficePolicyProvider = baseOfficePolicyProvider;
        _availabilityPolicyProvider = availabilityPolicyProvider;
        _routing = routing;
        _rankingPolicyProvider = rankingPolicyProvider;
        _ranking = ranking;
        _logger = logger;
    }

    public async Task<LocationSearchResponseV1> SearchInPersonAsync(LocationSearchRequestV1 req, CancellationToken ct)
    {
        var response = new LocationSearchResponseV1
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
        await ComputeNearestOfficeRouteAsync(ctx, ct);

        await BuildResponseCandidatesAsync(ctx, ct);
        RankAndApplyCandidates(ctx);

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
            ctx.Response.Warnings.Add(new ApiWarning
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
            ctx.Response.Warnings.Add(new ApiWarning
            {
                Code = "NO_CANDIDATES",
                Message = "No advisers matched the supplied filters."
            });
        }
    }

    private async Task LoadAvailabilityAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        var availability = await _calendar.GetAvailabilityAsync(
            ctx.Candidates.Select(x => x.Adviser.AdviserId).ToList(),
            ctx.Request.Meeting,
            ct);

        ctx.AvailabilityById = availability.ToDictionary(x => x.AdviserId, StringComparer.OrdinalIgnoreCase);
    }

    private async Task LoadPoliciesAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        ctx.CoveragePolicy = await _coveragePolicyProvider.GetAsync(ct);
        ctx.BaseOfficePolicy = await _baseOfficePolicyProvider.GetAsync(ct);
        ctx.RankingPolicy = await _rankingPolicyProvider.GetAsync(ct);
        ctx.AvailabilityPolicy = await _availabilityPolicyProvider.GetAsync(ct);
    }

    private async Task ResolveAdviserOriginsAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        var (destLat, destLng) = ctx.Destination;

        foreach (var c in ctx.Candidates)
        {
            ctx.AvailabilityById.TryGetValue(c.Adviser.AdviserId, out var availability);
            var origin = await _adviserCoords.ResolveHomeAsync(c.Adviser, ct, availability?.CurrentLocationPostcode);
            if (IsZero(origin)) continue;

            ctx.AdviserOrigins[c.Adviser.AdviserId] = origin;

            var air = CoverageEvaluator.HaversineMiles(origin.Lat, origin.Lng, destLat, destLng);
            ctx.AirMilesById[c.Adviser.AdviserId] = air;
        }
    }

    private async Task ComputeMatrixRoutesToClientAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        var (destLat, destLng) = ctx.Destination;

        var withinCoverage = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in ctx.Candidates)
        {
            if (!ctx.AdviserOrigins.TryGetValue(c.Adviser.AdviserId, out var origin))
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

    private async Task ComputeNearestOfficeRouteAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ctx.NearestOfficeId)) return;
        if (!ctx.OfficeCoords.TryGetValue(ctx.NearestOfficeId, out var office)) return;
        if (IsZero(office)) return;

        var (destLat, destLng) = ctx.Destination;

        try
        {
            ctx.NearestOfficeRoute = await _routing.GetRouteAsync((destLat, destLng), (office.Lat, office.Lng), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nearest office routing failed OfficeId={OfficeId}", ctx.NearestOfficeId);
        }
    }

    private async Task BuildResponseCandidatesAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        var (destLat, destLng) = ctx.Destination;

        foreach (var c in ctx.Candidates)
        {
            var reasons = new List<string>();
            AddBaseReasons(ctx, c, reasons);

            var (availStatus, proposedStartUtc) = EvaluateAvailability(ctx, c, reasons);

            var withinCoverage = AddCoverageReasons(ctx, c, reasons);

            var travelToClient = await BuildTravelToClient(ctx, c, withinCoverage, reasons, ct);

            var coverageDistanceMiles = travelToClient.DistanceMiles > 0
                ? travelToClient.DistanceMiles
                : (ctx.AirMilesById.TryGetValue(c.Adviser.AdviserId, out var air) ? Math.Round(air, 2) : 0d);

            var travelToBase = await BuildTravelToBase(ctx, c, reasons, ct);

            var travelToNearestOffice = new TravelToNearestOffice
            {
                OfficeId = ctx.NearestOfficeId ?? "TBC",
                EtaMinutes = ctx.NearestOfficeRoute?.EtaMinutes ?? 0,
                DistanceMiles = ctx.NearestOfficeRoute?.DistanceMiles,
                Confidence = ctx.NearestOfficeRoute?.Confidence
            };

            // ranking signals
            reasons.Add($"RANK_AVAILABLE_{(availStatus == "Available" ? "Y" : "N")}");
            reasons.Add($"RANK_ETA_{travelToClient.EtaMinutes}");
            reasons.Add($"RANK_DISTANCE_{travelToClient.DistanceMiles:0.##}");

            ctx.Response.Candidates.Add(new LocationCandidate
            {
                AdviserId = c.Adviser.AdviserId,
                AdviserRating = c.Adviser.Rating,
                Preferred = c.IsPreferred,
                Availability = availStatus,
                ProposedSlotUtc = new ProposedSlot
                {
                    Start = proposedStartUtc,
                    End = proposedStartUtc.AddMinutes(ctx.Request.Meeting.DurationMinutes)
                },
                Coverage = new CoverageInfo
                {
                    WithinCoverage = withinCoverage,
                    AnchorPostcode = c.Adviser.HomePostcode,
                    DistanceMiles = coverageDistanceMiles
                },
                TravelToClient = travelToClient,
                TravelToBase = travelToBase,
                TravelToNearestOffice = travelToNearestOffice,
                Reasons = reasons
            });
        }
    }

    private void RankAndApplyCandidates(LocationSearchContext ctx)
    {
        var ranked = ctx.Response.Candidates
            .Select(c => _ranking.Rank(c, ctx.RankingPolicy))
            .OrderBy(x => x.Score)
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

    private static void AddBaseReasons(LocationSearchContext ctx, AdviserCandidate c, List<string> reasons)
    {
        var (destLat, destLng) = ctx.Destination;

        reasons.Add("CANDIDATE_SOURCE");
        reasons.Add(c.IsPreferred ? "PREFERRED_ADVISER" : "NOT_PREFERRED");
        reasons.Add($"DEST_SOURCE_{ctx.DestResolved.Source}");
        reasons.Add($"REGION_{c.Adviser.Region}");
        reasons.Add($"DEST_{destLat:F4}_{destLng:F4}");

        if (ctx.AdviserOrigins.TryGetValue(c.Adviser.AdviserId, out var origin))
            reasons.Add($"ORIGIN_{origin.Lat:F4}_{origin.Lng:F4}");
        else
        {
            reasons.Add("ORIGIN_MISSING");
            reasons.Add("INVALID_HOME_POSTCODE");
        }
    }

    private static (string Status, DateTime ProposedStartUtc) EvaluateAvailability(
        LocationSearchContext ctx,
        AdviserCandidate c,
        List<string> reasons)
    {
        ctx.AvailabilityById.TryGetValue(c.Adviser.AdviserId, out var a);

        var busyBlocks = a?.BusyBlocks
            .Select(b => (b.StartUtc, b.EndUtc))
            .ToList()
            ?? new List<(DateTime StartUtc, DateTime EndUtc)>();

        var buffer = GetBufferMinutes(ctx);
        if (buffer > 0)
        {
            busyBlocks = busyBlocks
                .Select(b => (b.StartUtc.AddMinutes(-buffer), b.EndUtc.AddMinutes(buffer)))
                .ToList();
            reasons.Add($"AVAILABILITY_BUFFER_{buffer}");
        }

        var (status, proposedStart) = AvailabilityEvaluator.Evaluate(ctx.Request.Meeting, busyBlocks);

        reasons.Add($"AVAILABILITY_{status}");
        return (status, proposedStart);
    }

    private bool AddCoverageReasons(LocationSearchContext ctx, AdviserCandidate c, List<string> reasons)
    {
        var radius = GetRadiusMiles(ctx, c.Adviser.AdviserId, c.Adviser.Region);
        reasons.Add($"COVER_RADIUS_{radius:F0}");

        if (ctx.AirMilesById.TryGetValue(c.Adviser.AdviserId, out var air))
            reasons.Add($"AIR_{air:F1}");

        if (!ctx.AdviserOrigins.ContainsKey(c.Adviser.AdviserId))
        {
            reasons.Add("NO_ORIGIN_COORDS");
            return false;
        }

        var within = IsWithinCoverage(ctx, c.Adviser.AdviserId, c.Adviser.Region);
        if (!within) reasons.Add("OUT_OF_COVERAGE");
        return within;
    }

    private async Task<TravelToClient> BuildTravelToClient(
        LocationSearchContext ctx,
        AdviserCandidate c,
        bool withinCoverage,
        List<string> reasons,
        CancellationToken ct)
    {
        if (!withinCoverage)
            return new TravelToClient { EtaMinutes = 0, DistanceMiles = 0, Confidence = "Low" };

        // Primary: matrix
        if (ctx.RoutesToClient.TryGetValue(c.Adviser.AdviserId, out var rr) && rr.EtaMinutes > 0)
        {
            reasons.Add("ROUTE_OK");
            return new TravelToClient
            {
                EtaMinutes = rr.EtaMinutes,
                DistanceMiles = rr.DistanceMiles,
                Confidence = rr.Confidence
            };
        }

        // Fallback: single route
        if (!ctx.AdviserOrigins.TryGetValue(c.Adviser.AdviserId, out var origin))
        {
            reasons.Add("ROUTING_UNAVAILABLE");
            return new TravelToClient { EtaMinutes = 0, DistanceMiles = 0, Confidence = "Low" };
        }

        try
        {
            var (destLat, destLng) = ctx.Destination;
            var fb = await _routing.GetRouteAsync((origin.Lat, origin.Lng), (destLat, destLng), ct);

            if (fb.EtaMinutes > 0)
            {
                reasons.Add("ROUTE_FALLBACK_OK");
                return new TravelToClient
                {
                    EtaMinutes = fb.EtaMinutes,
                    DistanceMiles = fb.DistanceMiles,
                    Confidence = fb.Confidence
                };
            }

            reasons.Add("ROUTING_FAILED");
            return new TravelToClient { EtaMinutes = 0, DistanceMiles = 0, Confidence = "Low" };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Fallback routing failed AdviserId={AdviserId}", c.Adviser.AdviserId);
            reasons.Add("ROUTING_FAILED");
            return new TravelToClient { EtaMinutes = 0, DistanceMiles = 0, Confidence = "Low" };
        }
    }

    private async Task<TravelToBase> BuildTravelToBase(
        LocationSearchContext ctx,
        AdviserCandidate c,
        List<string> reasons,
        CancellationToken ct)
    {
        var (destLat, destLng) = ctx.Destination;
        var result = new TravelToBase { HomeMinutes = 0, OfficeMinutes = 0 };

        // client -> home
        if (ctx.AdviserOrigins.TryGetValue(c.Adviser.AdviserId, out var home))
        {
            result.HomeMinutes = await SafeEtaAsync((destLat, destLng), (home.Lat, home.Lng), "BASE_HOME", reasons, ct);
        }

        // client -> base office (region mapping)
        var baseOfficeId = ResolveBaseOfficeId(c.Adviser.Region, ctx.BaseOfficePolicy);
        if (!string.IsNullOrWhiteSpace(baseOfficeId) &&
            ctx.OfficeCoords.TryGetValue(baseOfficeId, out var office) &&
            !IsZero(office))
        {
            result.OfficeMinutes = await SafeEtaAsync((destLat, destLng), (office.Lat, office.Lng), "BASE_OFFICE", reasons, ct);
        }
        else
        {
            reasons.Add("BASE_OFFICE_UNKNOWN");
        }

        return result;
    }

    private async Task<int> SafeEtaAsync(
        (double Lat, double Lng) origin,
        (double Lat, double Lng) destination,
        string tag,
        List<string> reasons,
        CancellationToken ct)
    {
        try
        {
            var rr = await _routing.GetRouteAsync(origin, destination, ct);
            if (rr.EtaMinutes > 0) return rr.EtaMinutes;

            reasons.Add($"{tag}_ROUTE_ZERO");
            return 0;
        }
        catch
        {
            reasons.Add($"{tag}_ROUTE_FAILED");
            return 0;
        }
    }

    private static bool IsZero((double Lat, double Lng) x) => x.Lat == 0d && x.Lng == 0d;

    private static int GetBufferMinutes(LocationSearchContext ctx)
    {
        var requested = ctx.Request.Filters?.BufferMinutes;
        var effective = requested ?? ctx.AvailabilityPolicy.DefaultBufferMinutes;
        return Math.Clamp(effective, 0, Math.Max(0, ctx.AvailabilityPolicy.MaxBufferMinutes));
    }

    private static void ApplyCandidateEligibilityFilters(LocationSearchContext ctx)
    {
        var minRating = ctx.Request.Filters?.MinAdviserRating ?? ctx.RankingPolicy.DefaultMinAdviserRating;
        if (minRating <= 0) return;

        ctx.Candidates = ctx.Candidates
            .Where(c => c.Adviser.Rating >= minRating)
            .ToList();
    }

    private double GetRadiusMiles(LocationSearchContext ctx, string adviserId, string region)
    {
        if (ctx.CoveragePolicy.AdviserRadiusMiles.TryGetValue(adviserId, out var a))
            return a;

        if (ctx.CoveragePolicy.RegionRadiusMiles.TryGetValue(region, out var r))
            return r;

        return ctx.CoveragePolicy.DefaultRadiusMiles;
    }

    private bool IsWithinCoverage(LocationSearchContext ctx, string adviserId, string region)
    {
        var radius = GetRadiusMiles(ctx, adviserId, region);
        return ctx.AirMilesById.TryGetValue(adviserId, out var miles) && miles <= radius;
    }

    private static string ResolveBaseOfficeId(string region, BaseOfficePolicy policy)
    {
        if (policy.RegionOfficeMap.TryGetValue(region, out var officeId) && !string.IsNullOrWhiteSpace(officeId))
            return officeId;

        return policy.DefaultOfficeId;
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
