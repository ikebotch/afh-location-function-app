using AFH.Location.Service.Core.Contracts.V1.Requests;

namespace AFH.Location.Service.Core.Services.Common;

public static class AvailabilityEvaluator
{
    public static (string Status, DateTime ProposedStartUtc) Evaluate(
        MeetingWindow meeting,
        IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> busyBlocks)
    {
        var requestedStart = meeting.RequestedStartUtc;
        var duration = TimeSpan.FromMinutes(meeting.DurationMinutes);
        var horizonEnd = requestedStart.AddMinutes(meeting.SearchHorizonMinutes);

        bool Overlaps(DateTime s1, DateTime e1, DateTime s2, DateTime e2)
            => s1 < e2 && s2 < e1;

        bool IsFreeAt(DateTime start)
        {
            var end = start.Add(duration);
            return !busyBlocks.Any(b => Overlaps(start, end, b.StartUtc, b.EndUtc));
        }

        // Requested slot is free
        if (IsFreeAt(requestedStart))
            return ("Available", requestedStart);

        // Find next free slot in horizon (simple step search)
        var step = TimeSpan.FromMinutes(15);
        for (var t = requestedStart.Add(step); t.Add(duration) <= horizonEnd; t = t.Add(step))
        {
            if (IsFreeAt(t))
                return ("AvailableLater", t);
        }

        return ("Busy", requestedStart);
    }
}