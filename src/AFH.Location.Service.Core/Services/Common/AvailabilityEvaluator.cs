using AFH.Location.Service.Core.Contracts.V1.Requests;

namespace AFH.Location.Service.Core.Services.Common;

public static class AvailabilityEvaluator
{
    private static readonly TimeSpan BusinessDayStart = TimeSpan.FromHours(8);
    private static readonly TimeSpan BusinessDayEnd = TimeSpan.FromHours(17);
    private static readonly TimeZoneInfo LondonTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

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
            if (!IsWithinBusinessHours(start, end))
                return false;

            return !busyBlocks.Any(b => Overlaps(start, end, b.StartUtc, b.EndUtc));
        }

        DateTime NormalizeToBusinessStart(DateTime utc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), LondonTz);
            var localDate = local.Date;
            var localStart = localDate.Add(BusinessDayStart);

            if (local.TimeOfDay < BusinessDayStart)
                local = localStart;
            else if (local.TimeOfDay > BusinessDayEnd)
                local = localDate.AddDays(1).Add(BusinessDayStart);

            var roundedMinutes = ((local.Minute + 14) / 15) * 15;
            if (roundedMinutes == 60)
                local = new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0).AddHours(1);
            else
                local = new DateTime(local.Year, local.Month, local.Day, local.Hour, roundedMinutes, 0);

            return TimeZoneInfo.ConvertTimeToUtc(local, LondonTz);
        }

        bool IsWithinBusinessHours(DateTime utcStart, DateTime utcEnd)
        {
            var localStart = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcStart, DateTimeKind.Utc), LondonTz);
            var localEnd = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcEnd, DateTimeKind.Utc), LondonTz);

            if (localStart.Date != localEnd.Date)
                return false;

            return localStart.TimeOfDay >= BusinessDayStart && localEnd.TimeOfDay <= BusinessDayEnd;
        }

        // Requested slot is free
        var normalizedStart = NormalizeToBusinessStart(requestedStart);
        if (IsFreeAt(normalizedStart))
            return ("Available", normalizedStart);

        // Find next free slot in horizon (simple step search)
        var step = TimeSpan.FromMinutes(15);
        for (var t = normalizedStart.Add(step); t.Add(duration) <= horizonEnd; t = t.Add(step))
        {
            if (IsFreeAt(t))
                return ("AvailableLater", t);
        }

        return ("Busy", normalizedStart);
    }
}
