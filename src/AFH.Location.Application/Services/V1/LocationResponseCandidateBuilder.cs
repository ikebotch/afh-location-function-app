using AFH.Location.Application.Abstractions.Calendar;
using AFH.Location.Application.Common;
using AFH.Location.Application.Models.V1.Results;
using AFH.Location.Application.Services.Common;
using AFH.Location.Domain.Entities;

namespace AFH.Location.Application.Services.V1;

public sealed class LocationResponseCandidateBuilder
{
    private const int MaxParallelCandidateBuild = 6;
    private readonly AvailabilityEvaluator _availabilityEvaluator;
    private readonly LocationSearchRoutingCoordinator _routingCoordinator;

    public LocationResponseCandidateBuilder(
        AvailabilityEvaluator availabilityEvaluator,
        LocationSearchRoutingCoordinator routingCoordinator)
    {
        _availabilityEvaluator = availabilityEvaluator;
        _routingCoordinator = routingCoordinator;
    }

    internal async Task BuildAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        var built = new List<LocationSearchCandidate>();
        var sync = new object();
        using var gate = new SemaphoreSlim(MaxParallelCandidateBuild, MaxParallelCandidateBuild);

        var tasks = ctx.Candidates.Select(async candidate =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var reasons = new List<string>();
                AddBaseReasons(ctx, candidate, reasons);

                // SKILL FILTER

                var requiredSkillKeys =
                    SkillKeyNormaliser.ToSkillKeys(
                        ctx.Request.Filters?.RequiredSkills);

                var adviserSkillKeys =
                    SkillKeyNormaliser.ToSkillKeys(
                        candidate.Adviser.Skills);

                var hasRequiredSkills =
                    requiredSkillKeys.Count == 0 ||
                    requiredSkillKeys.All(adviserSkillKeys.Contains);

                reasons.Add(
                    requiredSkillKeys.Count == 0
                        ? "NO_REQUIRED_SKILLS"
                        : $"REQUIRED_SKILLS_{string.Join(",", requiredSkillKeys)}");

                reasons.Add(
                    adviserSkillKeys.Count == 0
                        ? "ADVISER_SKILLS_NONE"
                        : $"ADVISER_SKILLS_{string.Join(",", adviserSkillKeys)}");

                if (!hasRequiredSkills)
                {
                    reasons.Add("MISSING_REQUIRED_SKILLS");
                    return;
                }

                reasons.Add("REQUIRED_SKILLS_MATCHED");

                var (availabilityStatus, proposedStartUtc) = EvaluateAvailability(ctx, candidate, reasons);
                if (!ShouldIncludeByAvailability(availabilityStatus))
                {
                    reasons.Add($"EXCLUDE_AVAILABILITY_{availabilityStatus.ToUpperInvariant()}");
                    return;
                }

                var unavailableForRouting =
                    string.Equals(availabilityStatus, "Unavailable", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(availabilityStatus, "Busy", StringComparison.OrdinalIgnoreCase);

                var withinCoverageByRadius = !unavailableForRouting && AddCoverageReasons(ctx, candidate, reasons);

                var maxTravelTimeMinutes = GetMaxTravelTimeMinutes(ctx, candidate.Adviser.AdviserId, candidate.Adviser.Region);
                reasons.Add($"MAX_TRAVEL_TIME_{maxTravelTimeMinutes}");

                var travelToClient = unavailableForRouting
                    ? new TravelToClientResult { EtaMinutes = null, DistanceMiles = null, Confidence = "Low" }
                    : await _routingCoordinator.BuildTravelToClientAsync(ctx, candidate, withinCoverageByRadius, reasons, ct);

                if (unavailableForRouting)
                    reasons.Add("SKIP_ROUTING_UNAVAILABLE");

                var withinCoverageByTravelTime = IsWithinMaxTravelTime(travelToClient, maxTravelTimeMinutes, reasons);
                var withinCoverage = withinCoverageByRadius && withinCoverageByTravelTime;

                var coverageDistanceMiles = travelToClient.DistanceMiles > 0
                    ? travelToClient.DistanceMiles.Value
                    : (ctx.AirMilesById.TryGetValue(candidate.Adviser.AdviserId, out var air) ? Math.Round(air, 2) : 0d);

                var travelBufferMinutes = GetTravelBufferMinutes(ctx);
                var companyBufferMinutes = GetCompanyBufferMinutes(ctx);
                var preMeetingBufferMinutes = (travelToClient.EtaMinutes ?? 0) + companyBufferMinutes;
                var postMeetingBufferMinutes = companyBufferMinutes;

                ApplyCompanyBufferAvailabilityRules(
                    ctx,
                    candidate.Adviser.AdviserId,
                    preMeetingBufferMinutes,
                    postMeetingBufferMinutes,
                    ref availabilityStatus,
                    ref proposedStartUtc,
                    reasons);

                var travelToBase = unavailableForRouting
                    ? new TravelToBaseResult { HomeMinutes = 0, OfficeMinutes = 0 }
                    : await _routingCoordinator.BuildTravelToBaseAsync(ctx, candidate, reasons, ct);

                var travelToNearestOffice = new TravelToNearestOfficeResult
                {
                    OfficeId = ctx.NearestOfficeId ?? "TBC",
                    EtaMinutes = ctx.NearestOfficeRoute?.EtaMinutes ?? 0,
                    DistanceMiles = ctx.NearestOfficeRoute?.DistanceMiles,
                    Confidence = ctx.NearestOfficeRoute?.Confidence
                };

                reasons.Add($"RANK_AVAILABLE_{(availabilityStatus == "Available" ? "Y" : "N")}");
                reasons.Add(travelToClient.EtaMinutes.HasValue
                    ? $"RANK_ETA_{travelToClient.EtaMinutes.Value}"
                    : "RANK_ETA_UNVERIFIED");
                reasons.Add(travelToClient.DistanceMiles.HasValue
                    ? $"RANK_DISTANCE_{travelToClient.DistanceMiles.Value:0.##}"
                    : "RANK_DISTANCE_UNVERIFIED");

                var responseCandidate = new LocationSearchCandidate
                {
                    AdviserId = candidate.Adviser.AdviserId,
                    MailboxUserId = string.IsNullOrWhiteSpace(candidate.Adviser.MailboxUserId) ? candidate.Adviser.AdviserId : candidate.Adviser.MailboxUserId.Trim(),
                    AdviserRating = candidate.Adviser.Rating,
                    GoldStar = candidate.Adviser.Rating >= 5d,
                    Preferred = candidate.IsPreferred,
                    Availability = availabilityStatus,
                    ProposedSlotUtc = new ProposedSlotResult
                    {
                        Start = proposedStartUtc,
                        End = proposedStartUtc.AddMinutes(ctx.Request.Meeting.DurationMinutes)
                    },
                    Coverage = new CoverageResult
                    {
                        WithinCoverage = withinCoverage,
                        AnchorPostcode = candidate.Adviser.HomePostcode,
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
                    TravelSnapshot = new TravelSnapshotResult
                    {
                        SourceLocationRef = candidate.Adviser.AdviserId,
                        SourcePostcode = candidate.Adviser.HomePostcode,
                        DestinationLocationRef = ctx.Request.RequestId,
                        DestinationPostcode = ctx.Request.Destination.Address?.Postcode,
                        TravelMinutes = travelToClient.EtaMinutes,
                        DistanceMiles = travelToClient.DistanceMiles,
                        Provider = "LocationService",
                        Confidence = travelToClient.Confidence,
                        CalculatedUtc = DateTime.UtcNow
                    },
                    Reasons = reasons
                };

                lock (sync)
                    built.Add(responseCandidate);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        ctx.Response.Candidates = built;
    }

    private static void AddBaseReasons(LocationSearchContext ctx, AdviserCandidate candidate, List<string> reasons)
    {
        var (destLat, destLng) = ctx.Destination;

        reasons.Add("CANDIDATE_SOURCE");
        reasons.Add(candidate.IsPreferred ? "PREFERRED_ADVISER" : "NOT_PREFERRED");
        reasons.Add($"DEST_SOURCE_{ctx.DestResolved.Source}");
        reasons.Add($"REGION_{candidate.Adviser.Region}");
        reasons.Add($"DEST_{destLat:F4}_{destLng:F4}");

        if (ctx.AdviserOrigins.TryGetValue(candidate.Adviser.AdviserId, out var origin))
            reasons.Add($"ORIGIN_{origin.Lat:F4}_{origin.Lng:F4}");
        else
        {
            reasons.Add("ORIGIN_MISSING");
            reasons.Add("INVALID_HOME_POSTCODE");
        }

        if (ctx.OriginSourceById.TryGetValue(candidate.Adviser.AdviserId, out var originSource))
            reasons.Add($"ORIGIN_SOURCE_{originSource}");
    }

    private (string Status, DateTime ProposedStartUtc) EvaluateAvailability(
        LocationSearchContext ctx,
        AdviserCandidate candidate,
        List<string> reasons)
    {
        ctx.AvailabilityById.TryGetValue(candidate.Adviser.AdviserId, out var availability);
        if (ctx.AvailabilityPolicy.RequireCalendarAvailability)
        {
            if (availability is null)
            {
                AddWarningOnce(ctx, "CALENDAR_UNAVAILABLE", "Calendar availability could not be resolved for one or more advisers.");
                reasons.Add("CALENDAR_AVAILABILITY_MISSING");
                return ("Unavailable", ctx.Request.Meeting.RequestedStartUtc);
            }

            if (availability.State is CalendarAvailabilityState.MailboxNotFound)
            {
                AddWarningOnce(ctx, "MAILBOX_NOT_FOUND", "One or more advisers do not have a mailbox or calendar schedule.");
                reasons.Add("MAILBOX_NOT_FOUND");
                return ("Unavailable", ctx.Request.Meeting.RequestedStartUtc);
            }

            if (availability.State is not CalendarAvailabilityState.Ok)
            {
                AddWarningOnce(ctx, "CALENDAR_UNAVAILABLE", "Calendar service could not be reached for one or more advisers.");
                reasons.Add($"CALENDAR_STATE_{availability.State}");
                return ("Unavailable", ctx.Request.Meeting.RequestedStartUtc);
            }
        }

        var busyBlocks = availability?.BusyBlocks
            .Select(block => (block.StartUtc, block.EndUtc))
            .ToList()
            ?? [];

        var travelBuffer = GetTravelBufferMinutes(ctx);
        if (travelBuffer > 0)
        {
            busyBlocks = busyBlocks
                .Select(block => (block.StartUtc.AddMinutes(-travelBuffer), block.EndUtc.AddMinutes(travelBuffer)))
                .ToList();
            reasons.Add($"AVAILABILITY_TRAVEL_BUFFER_{travelBuffer}");
        }

        var (status, proposedStartUtc) = _availabilityEvaluator.Evaluate(ctx.Request.Meeting, busyBlocks);

        reasons.Add($"AVAILABILITY_{status}");
        return (status, proposedStartUtc);
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

    private bool AddCoverageReasons(LocationSearchContext ctx, AdviserCandidate candidate, List<string> reasons)
    {
        var radius = GetRadiusMiles(ctx, candidate.Adviser.AdviserId, candidate.Adviser.Region);
        reasons.Add($"COVER_RADIUS_{radius:F0}");

        if (ctx.AirMilesById.TryGetValue(candidate.Adviser.AdviserId, out var air))
            reasons.Add($"AIR_{air:F1}");

        if (!ctx.AdviserOrigins.ContainsKey(candidate.Adviser.AdviserId))
        {
            reasons.Add("NO_ORIGIN_COORDS");
            return false;
        }

        var withinCoverage = IsWithinCoverage(ctx, candidate.Adviser.AdviserId, candidate.Adviser.Region);
        if (!withinCoverage) reasons.Add("OUT_OF_COVERAGE");
        return withinCoverage;
    }

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

    private double GetRadiusMiles(LocationSearchContext ctx, string adviserId, string region)
    {
        if (ctx.CoveragePolicy.AdviserRadiusMiles.TryGetValue(adviserId, out var adviserRadius))
            return adviserRadius;

        if (ctx.CoveragePolicy.RegionRadiusMiles.TryGetValue(region, out var regionRadius))
            return regionRadius;

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
        if (!travelToClient.EtaMinutes.HasValue || travelToClient.EtaMinutes.Value <= 0)
        {
            reasons.Add("MAX_TRAVEL_TIME_UNVERIFIED");
            return false;
        }

        var within = travelToClient.EtaMinutes.Value <= maxTravelTimeMinutes;
        reasons.Add(within
            ? $"MAX_TRAVEL_TIME_OK_{travelToClient.EtaMinutes.Value}"
            : $"MAX_TRAVEL_TIME_EXCEEDED_{travelToClient.EtaMinutes.Value}");

        return within;
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
}