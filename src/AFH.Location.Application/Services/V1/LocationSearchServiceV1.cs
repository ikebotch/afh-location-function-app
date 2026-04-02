using AFH.Location.Application.Abstractions;
using AFH.Location.Application.Models.V1;
using AFH.Location.Application.Services.Common;
using AFH.Location.Domain;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AFH.Location.Application.Services.V1;

public sealed class LocationSearchServiceV1 : ILocationSearchService
{
    private const int MaxParallelOriginResolutions = 8;
    private const int MaxParallelCandidateBuild = 6;
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
    private readonly ISearchAuditRepository _searchAuditRepository;
    private readonly AvailabilityEvaluator _availabilityEvaluator;

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
        ISearchAuditRepository searchAuditRepository,
        AvailabilityEvaluator availabilityEvaluator,
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
        _searchAuditRepository = searchAuditRepository;
        _availabilityEvaluator = availabilityEvaluator;
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
        await ComputeNearestOfficeRouteAsync(ctx, ct);

        await BuildResponseCandidatesAsync(ctx, ct);
        RankAndApplyCandidates(ctx);
        await PersistSearchAuditAsync(ctx, ct);

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

    private async Task ComputeNearestOfficeRouteAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        if (ctx.AvailabilityPolicy.RequireCalendarAvailability &&
            !ctx.Candidates.Any(c =>
                ctx.AvailabilityById.TryGetValue(c.Adviser.AdviserId, out var a) &&
                a.State == CalendarAvailabilityState.Ok))
        {
            return;
        }

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
        var built = new List<LocationSearchCandidate>();
        var sync = new object();
        using var gate = new SemaphoreSlim(MaxParallelCandidateBuild, MaxParallelCandidateBuild);

        var tasks = ctx.Candidates.Select(async c =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var reasons = new List<string>();
                AddBaseReasons(ctx, c, reasons);

                var (availStatus, proposedStartUtc) = EvaluateAvailability(ctx, c, reasons);
                if (!ShouldIncludeByAvailability(availStatus))
                {
                    reasons.Add($"EXCLUDE_AVAILABILITY_{availStatus.ToUpperInvariant()}");
                    return;
                }

                var unavailableForRouting =
                    string.Equals(availStatus, "Unavailable", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(availStatus, "Busy", StringComparison.OrdinalIgnoreCase);

                var withinCoverageByRadius = !unavailableForRouting && AddCoverageReasons(ctx, c, reasons);

                var maxTravelTimeMinutes = GetMaxTravelTimeMinutes(ctx, c.Adviser.AdviserId, c.Adviser.Region);
                reasons.Add($"MAX_TRAVEL_TIME_{maxTravelTimeMinutes}");

                TravelToClientResult travelToClient;
                if (unavailableForRouting)
                {
                    reasons.Add("SKIP_ROUTING_UNAVAILABLE");
                    travelToClient = new TravelToClientResult { EtaMinutes = 0, DistanceMiles = 0, Confidence = "Low" };
                }
                else
                {
                    travelToClient = await BuildTravelToClient(ctx, c, withinCoverageByRadius, reasons, ct);
                }

                var withinCoverageByTravelTime = IsWithinMaxTravelTime(travelToClient, maxTravelTimeMinutes, reasons);
                var withinCoverage = withinCoverageByRadius && withinCoverageByTravelTime;

                var coverageDistanceMiles = travelToClient.DistanceMiles > 0
                    ? travelToClient.DistanceMiles
                    : (ctx.AirMilesById.TryGetValue(c.Adviser.AdviserId, out var air) ? Math.Round(air, 2) : 0d);

                var travelBufferMinutes = GetTravelBufferMinutes(ctx);
                var companyBufferMinutes = GetCompanyBufferMinutes(ctx);
                var preMeetingBufferMinutes = travelToClient.EtaMinutes + companyBufferMinutes;
                var postMeetingBufferMinutes = companyBufferMinutes;

                ApplyCompanyBufferAvailabilityRules(
                    ctx,
                    c.Adviser.AdviserId,
                    preMeetingBufferMinutes,
                    postMeetingBufferMinutes,
                    ref availStatus,
                    ref proposedStartUtc,
                    reasons);

                var travelToBase = unavailableForRouting
                    ? new TravelToBaseResult { HomeMinutes = 0, OfficeMinutes = 0 }
                    : await BuildTravelToBase(ctx, c, reasons, ct);

                var travelToNearestOffice = new TravelToNearestOfficeResult
                {
                    OfficeId = ctx.NearestOfficeId ?? "TBC",
                    EtaMinutes = ctx.NearestOfficeRoute?.EtaMinutes ?? 0,
                    DistanceMiles = ctx.NearestOfficeRoute?.DistanceMiles,
                    Confidence = ctx.NearestOfficeRoute?.Confidence
                };

                reasons.Add($"RANK_AVAILABLE_{(availStatus == "Available" ? "Y" : "N")}");
                reasons.Add($"RANK_ETA_{travelToClient.EtaMinutes}");
                reasons.Add($"RANK_DISTANCE_{travelToClient.DistanceMiles:0.##}");

                var candidate = new LocationSearchCandidate
                {
                    AdviserId = c.Adviser.AdviserId,
                    MailboxUserId = string.IsNullOrWhiteSpace(c.Adviser.MailboxUserId) ? c.Adviser.AdviserId : c.Adviser.MailboxUserId.Trim(),
                    AdviserRating = c.Adviser.Rating,
                    GoldStar = c.Adviser.Rating >= 5d,
                    Preferred = c.IsPreferred,
                    Availability = availStatus,
                    ProposedSlotUtc = new ProposedSlotResult
                    {
                        Start = proposedStartUtc,
                        End = proposedStartUtc.AddMinutes(ctx.Request.Meeting.DurationMinutes)
                    },
                    Coverage = new CoverageResult
                    {
                        WithinCoverage = withinCoverage,
                        AnchorPostcode = c.Adviser.HomePostcode,
                        DistanceMiles = coverageDistanceMiles
                    },
                    TravelToClient = travelToClient,
                    TravelToBase = travelToBase,
                    TravelToNearestOffice = travelToNearestOffice,
                    Buffers = new BufferResult
                    {
                        TravelBufferMinutes = travelBufferMinutes,
                        CompanyBufferMinutes = companyBufferMinutes,
                        PreMeetingBufferMinutes = preMeetingBufferMinutes,
                        PostMeetingBufferMinutes = postMeetingBufferMinutes,
                        MaxTravelTimeMinutes = maxTravelTimeMinutes
                    },
                    Reasons = reasons
                };

                lock (sync)
                    built.Add(candidate);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        ctx.Response.Candidates = built;
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

        if (ctx.OriginSourceById.TryGetValue(c.Adviser.AdviserId, out var originSource))
            reasons.Add($"ORIGIN_SOURCE_{originSource}");
    }

    private (string Status, DateTime ProposedStartUtc) EvaluateAvailability(
        LocationSearchContext ctx,
        AdviserCandidate c,
        List<string> reasons)
    {
        ctx.AvailabilityById.TryGetValue(c.Adviser.AdviserId, out var a);
        if (ctx.AvailabilityPolicy.RequireCalendarAvailability)
        {
            if (a is null)
            {
                AddWarningOnce(ctx, "CALENDAR_UNAVAILABLE", "Calendar availability could not be resolved for one or more advisers.");
                reasons.Add("CALENDAR_AVAILABILITY_MISSING");
                return ("Unavailable", ctx.Request.Meeting.RequestedStartUtc);
            }

            if (a.State is CalendarAvailabilityState.MailboxNotFound)
            {
                AddWarningOnce(ctx, "MAILBOX_NOT_FOUND", "One or more advisers do not have a mailbox or calendar schedule.");
                reasons.Add("MAILBOX_NOT_FOUND");
                return ("Unavailable", ctx.Request.Meeting.RequestedStartUtc);
            }

            if (a.State is not CalendarAvailabilityState.Ok)
            {
                AddWarningOnce(ctx, "CALENDAR_UNAVAILABLE", "Calendar service could not be reached for one or more advisers.");
                reasons.Add($"CALENDAR_STATE_{a.State}");
                return ("Unavailable", ctx.Request.Meeting.RequestedStartUtc);
            }
        }

        var busyBlocks = a?.BusyBlocks
            .Select(b => (b.StartUtc, b.EndUtc))
            .ToList()
            ?? new List<(DateTime StartUtc, DateTime EndUtc)>();

        var travelBuffer = GetTravelBufferMinutes(ctx);
        if (travelBuffer > 0)
        {
            busyBlocks = busyBlocks
                .Select(b => (b.StartUtc.AddMinutes(-travelBuffer), b.EndUtc.AddMinutes(travelBuffer)))
                .ToList();
            reasons.Add($"AVAILABILITY_TRAVEL_BUFFER_{travelBuffer}");
        }

        var (status, proposedStart) = _availabilityEvaluator.Evaluate(ctx.Request.Meeting, busyBlocks);

        reasons.Add($"AVAILABILITY_{status}");
        return (status, proposedStart);
    }

    private static void AddWarningOnce(LocationSearchContext ctx, string code, string message)
    {
        if (ctx.Response.Warnings.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
            return;

        ctx.Response.Warnings.Add(new LocationSearchWarning
        {
            Code = code,
            Message = message
        });
    }

    private static bool ShouldIncludeByAvailability(string availabilityStatus) =>
        string.Equals(availabilityStatus, "Available", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(availabilityStatus, "AvailableLater", StringComparison.OrdinalIgnoreCase);

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

    private async Task<TravelToClientResult> BuildTravelToClient(
        LocationSearchContext ctx,
        AdviserCandidate c,
        bool withinCoverage,
        List<string> reasons,
        CancellationToken ct)
    {
        if (!withinCoverage)
            return new TravelToClientResult { EtaMinutes = 0, DistanceMiles = 0, Confidence = "Low" };

        // Primary: matrix
        if (ctx.RoutesToClient.TryGetValue(c.Adviser.AdviserId, out var rr) && rr.EtaMinutes > 0)
        {
            reasons.Add("ROUTE_OK");
            return new TravelToClientResult
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
            return new TravelToClientResult { EtaMinutes = 0, DistanceMiles = 0, Confidence = "Low" };
        }

        try
        {
            var (destLat, destLng) = ctx.Destination;
            var fb = await _routing.GetRouteAsync((origin.Lat, origin.Lng), (destLat, destLng), ct);

            if (fb.EtaMinutes > 0)
            {
                reasons.Add("ROUTE_FALLBACK_OK");
                return new TravelToClientResult
                {
                    EtaMinutes = fb.EtaMinutes,
                    DistanceMiles = fb.DistanceMiles,
                    Confidence = fb.Confidence
                };
            }

            reasons.Add("ROUTING_FAILED");
            return new TravelToClientResult { EtaMinutes = 0, DistanceMiles = 0, Confidence = "Low" };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Fallback routing failed AdviserId={AdviserId}", c.Adviser.AdviserId);
            reasons.Add("ROUTING_FAILED");
            return new TravelToClientResult { EtaMinutes = 0, DistanceMiles = 0, Confidence = "Low" };
        }
    }

    private async Task<TravelToBaseResult> BuildTravelToBase(
        LocationSearchContext ctx,
        AdviserCandidate c,
        List<string> reasons,
        CancellationToken ct)
    {
        var (destLat, destLng) = ctx.Destination;
        var result = new TravelToBaseResult { HomeMinutes = 0, OfficeMinutes = 0 };

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
            if (!ctx.OfficeRouteMinutesByOfficeId.TryGetValue(baseOfficeId, out var officeMinutes))
            {
                officeMinutes = await SafeEtaAsync((destLat, destLng), (office.Lat, office.Lng), "BASE_OFFICE", reasons, ct);
                ctx.OfficeRouteMinutesByOfficeId.TryAdd(baseOfficeId, officeMinutes);
            }

            result.OfficeMinutes = officeMinutes;
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

    private static int GetTravelBufferMinutes(LocationSearchContext ctx)
    {
        var requested = ctx.Request.Filters?.BufferMinutes;
        var effective = requested ?? ctx.AvailabilityPolicy.DefaultBufferMinutes;
        return Math.Clamp(effective, 0, Math.Max(0, ctx.AvailabilityPolicy.MaxBufferMinutes));
    }

    private static int GetCompanyBufferMinutes(LocationSearchContext ctx)
    {
        const int fallbackDefaultMinutes = 30;
        var requested = ctx.Request.Filters?.CompanyBufferMinutes;
        var policyDefault = ctx.AvailabilityPolicy.DefaultCompanyBufferMinutes > 0
            ? ctx.AvailabilityPolicy.DefaultCompanyBufferMinutes
            : fallbackDefaultMinutes;

        var effective = requested ?? policyDefault;
        return Math.Clamp(effective, 0, Math.Max(0, ctx.AvailabilityPolicy.MaxCompanyBufferMinutes));
    }

    private static int GetAvailabilitySearchExtensionMinutes(LocationSearchContext ctx)
    {
        var travel = GetTravelBufferMinutes(ctx);
        var company = GetCompanyBufferMinutes(ctx);
        return Math.Max(0, travel) + Math.Max(0, company);
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

    private int GetMaxTravelTimeMinutes(LocationSearchContext ctx, string adviserId, string region)
    {
        if (ctx.CoveragePolicy.AdviserMaxTravelTimeMinutes.TryGetValue(adviserId, out var adviserMax))
            return adviserMax;

        if (ctx.CoveragePolicy.RegionMaxTravelTimeMinutes.TryGetValue(region, out var regionMax))
            return regionMax;

        return ctx.CoveragePolicy.DefaultMaxTravelTimeMinutes;
    }

    private bool IsWithinCoverage(LocationSearchContext ctx, string adviserId, string region)
    {
        var radius = GetRadiusMiles(ctx, adviserId, region);
        return ctx.AirMilesById.TryGetValue(adviserId, out var miles) && miles <= radius;
    }

    private static bool IsWithinMaxTravelTime(TravelToClientResult travelToClient, int maxTravelTimeMinutes, List<string> reasons)
    {
        if (travelToClient.EtaMinutes <= 0)
        {
            reasons.Add("MAX_TRAVEL_TIME_UNVERIFIED");
            return false;
        }

        var within = travelToClient.EtaMinutes <= maxTravelTimeMinutes;
        reasons.Add(within
            ? $"MAX_TRAVEL_TIME_OK_{travelToClient.EtaMinutes}"
            : $"MAX_TRAVEL_TIME_EXCEEDED_{travelToClient.EtaMinutes}");

        return within;
    }

    private static bool IsCalendarRoutingEligible(LocationSearchContext ctx, string adviserId)
    {
        if (!ctx.AvailabilityPolicy.RequireCalendarAvailability)
            return true;

        if (!ctx.AvailabilityById.TryGetValue(adviserId, out var availability))
            return false;

        return availability.State == CalendarAvailabilityState.Ok;
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

    private static void ApplyCompanyBufferAvailabilityRules(
        LocationSearchContext ctx,
        string adviserId,
        int preMeetingBufferMinutes,
        int postMeetingBufferMinutes,
        ref string availabilityStatus,
        ref DateTime proposedStartUtc,
        List<string> reasons)
    {
        if (!ctx.AvailabilityById.TryGetValue(adviserId, out var availability))
            return;

        if (availability.BusyBlocks.Count == 0)
            return;

        var durationMinutes = ctx.Request.Meeting.DurationMinutes;
        var proposedEndUtc = proposedStartUtc.AddMinutes(durationMinutes);
        var requestedStartUtc = ctx.Request.Meeting.RequestedStartUtc;
        var latestAllowedUtc = requestedStartUtc.AddMinutes(Math.Max(1, ctx.Request.Meeting.SearchHorizonMinutes));

        var searchStartUtc = proposedStartUtc;
        var previous = availability.BusyBlocks
            .Where(x => x.EndUtc <= searchStartUtc)
            .OrderByDescending(x => x.EndUtc)
            .FirstOrDefault();

        if (previous is not null)
        {
            var gapBefore = (int)Math.Max(0, (proposedStartUtc - previous.EndUtc).TotalMinutes);
            if (gapBefore < preMeetingBufferMinutes)
            {
                var shiftedStart = previous.EndUtc.AddMinutes(preMeetingBufferMinutes);
                var shiftedEnd = shiftedStart.AddMinutes(durationMinutes);
                var overlapsShifted = availability.BusyBlocks.Any(b => shiftedStart < b.EndUtc && shiftedEnd > b.StartUtc);

                if (!overlapsShifted && shiftedStart <= latestAllowedUtc)
                {
                    availabilityStatus = "AvailableLater";
                    proposedStartUtc = shiftedStart;
                    proposedEndUtc = shiftedEnd;
                    reasons.Add($"COMPANY_BUFFER_SHIFTED_{preMeetingBufferMinutes}");
                }
                else
                {
                    availabilityStatus = "Busy";
                    reasons.Add($"COMPANY_BUFFER_PRE_FAIL_{preMeetingBufferMinutes}_{gapBefore}");
                    return;
                }
            }
        }

        var next = availability.BusyBlocks
            .Where(x => x.StartUtc >= proposedEndUtc)
            .OrderBy(x => x.StartUtc)
            .FirstOrDefault();

        if (next is null) return;

        var gapAfter = (int)Math.Max(0, (next.StartUtc - proposedEndUtc).TotalMinutes);
        if (gapAfter < postMeetingBufferMinutes)
        {
            availabilityStatus = "Busy";
            reasons.Add($"COMPANY_BUFFER_POST_FAIL_{postMeetingBufferMinutes}_{gapAfter}");
        }
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

    private async Task PersistSearchAuditAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        try
        {
            var selected = ctx.Response.Candidates
                .OrderBy(x => x.Rank)
                .FirstOrDefault();

            var payload = ctx.Response.Candidates
                .Select(x => new
                {
                    x.AdviserId,
                    x.Rank,
                    x.Score,
                    x.AdviserRating,
                    x.GoldStar,
                    x.Availability,
                    x.Coverage.WithinCoverage,
                    TravelEtaMinutes = x.TravelToClient.EtaMinutes,
                    x.Buffers.MaxTravelTimeMinutes,
                    x.Buffers.CompanyBufferMinutes,
                    x.Buffers.TravelBufferMinutes,
                    OriginSource = x.Reasons.FirstOrDefault(r => r.StartsWith("ORIGIN_SOURCE_", StringComparison.OrdinalIgnoreCase)),
                    FailureReasons = x.Reasons.Where(r =>
                        r.Contains("EXCEEDED", StringComparison.OrdinalIgnoreCase) ||
                        r.Contains("FAIL", StringComparison.OrdinalIgnoreCase) ||
                        r.Contains("OUT_OF_COVERAGE", StringComparison.OrdinalIgnoreCase))
                })
                .ToList();

            var entry = new SearchAuditEntry
            {
                RequestId = string.IsNullOrWhiteSpace(ctx.Request.RequestId) ? Guid.NewGuid().ToString("N") : ctx.Request.RequestId,
                CreatedUtc = DateTime.UtcNow,
                RequestedStartUtc = ctx.Request.Meeting.RequestedStartUtc,
                DurationMinutes = ctx.Request.Meeting.DurationMinutes,
                SearchHorizonMinutes = ctx.Request.Meeting.SearchHorizonMinutes,
                DestinationPostcode = ctx.Request.Destination.Address?.Postcode,
                RegionsCsv = ctx.Request.Filters?.Regions is { Length: > 0 } regions ? string.Join(",", regions) : null,
                CandidatesReturned = ctx.Response.Candidates.Count,
                SelectedAdviserId = selected?.AdviserId,
                SelectedAdviserRating = selected?.AdviserRating,
                SelectedAdviserGoldStar = selected?.GoldStar,
                SelectedTravelMinutes = selected?.TravelToClient.EtaMinutes,
                SelectedMaxTravelTimeMinutes = selected?.Buffers.MaxTravelTimeMinutes,
                SelectedCompanyBufferMinutes = selected?.Buffers.CompanyBufferMinutes,
                SelectedTravelBufferMinutes = selected?.Buffers.TravelBufferMinutes,
                SelectedOriginSource = selected?.Reasons.FirstOrDefault(r =>
                    r.StartsWith("ORIGIN_SOURCE_", StringComparison.OrdinalIgnoreCase)),
                PayloadJson = JsonSerializer.Serialize(payload)
            };

            await _searchAuditRepository.SaveAsync(entry, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search audit persistence failed. RequestId={RequestId}", ctx.Request.RequestId);
        }
    }
}
