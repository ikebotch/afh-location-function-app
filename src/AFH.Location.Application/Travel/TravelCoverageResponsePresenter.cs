namespace AFH.Location.Application.Travel;

public static class TravelCoverageResponsePresenter
{
    public static IReadOnlyList<TravelCoveragePresentedSlot>? PresentSlots(
        TravelCoverageSlotResponseMode responseMode,
        TravelCoverageTimeContext timeContext,
        TravelCoverageDestinationOutcome destination)
    {
        if (destination.Status != TravelCoverageStatus.Succeeded)
        {
            return null;
        }

        var sourceSlots = destination.Slots;
        if ((sourceSlots == null || sourceSlots.Count == 0) && destination.Route != null)
        {
            sourceSlots =
            [
                new TravelCoverageSlotOutcome
                {
                    StartTime = timeContext.RequestedDepartureTime ?? timeContext.StartTime,
                    EndTime = timeContext.EndTime,
                    Route = destination.Route,
                    Coverage = destination.Coverage
                }
            ];
        }

        if (sourceSlots == null || sourceSlots.Count == 0)
        {
            return [];
        }

        var rawSlots = sourceSlots.Select(s => new TravelCoveragePresentedSlot
        {
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            TravelTimeMinutes = s.Route?.TravelTimeMinutes ?? 0,
            TravelDistanceMiles = s.Route?.DistanceMiles ?? 0d,
            IsWithinCoverage = s.Coverage?.IsWithinCoverage ?? false
        }).ToList();

        if (responseMode == TravelCoverageSlotResponseMode.Expanded)
        {
            return rawSlots;
        }

        if (responseMode == TravelCoverageSlotResponseMode.Summary)
        {
            var minStart = rawSlots.Min(s => s.StartTime) ?? timeContext.StartTime;
            var maxEnd = rawSlots.Max(s => s.EndTime) ?? timeContext.EndTime;
            var maxTravelTime = rawSlots.Count > 0 ? rawSlots.Max(s => s.TravelTimeMinutes) : 0;
            var maxDistance = rawSlots.Count > 0 ? rawSlots.Max(s => s.TravelDistanceMiles) : 0d;
            var allWithinCoverage = rawSlots.Count > 0 && rawSlots.All(s => s.IsWithinCoverage);

            return
            [
                new TravelCoveragePresentedSlot
                {
                    StartTime = minStart,
                    EndTime = maxEnd,
                    TravelTimeMinutes = maxTravelTime,
                    TravelDistanceMiles = maxDistance,
                    IsWithinCoverage = allWithinCoverage
                }
            ];
        }

        var mergedSlots = new List<TravelCoveragePresentedSlot>();
        var currentMerged = rawSlots[0];
        for (var i = 1; i < rawSlots.Count; i++)
        {
            var nextSlot = rawSlots[i];
            if (currentMerged.TravelTimeMinutes == nextSlot.TravelTimeMinutes &&
                currentMerged.TravelDistanceMiles == nextSlot.TravelDistanceMiles &&
                currentMerged.IsWithinCoverage == nextSlot.IsWithinCoverage)
            {
                currentMerged = currentMerged with { EndTime = nextSlot.EndTime };
            }
            else
            {
                mergedSlots.Add(currentMerged);
                currentMerged = nextSlot;
            }
        }

        mergedSlots.Add(currentMerged);
        return mergedSlots;
    }
}
