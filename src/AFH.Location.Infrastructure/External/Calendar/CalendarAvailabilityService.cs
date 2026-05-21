using AFH.Location.Application.Abstractions.Calendar;
using AFH.Location.Application.Calendar;

namespace AFH.Location.Infrastructure.External.Calendar;

public sealed class CalendarAvailabilityService : ICalendarAvailabilityService
{
    private readonly ICalendarServiceClient _calendarClient;

    public CalendarAvailabilityService(ICalendarServiceClient calendarClient)
    {
        _calendarClient = calendarClient;
    }

    public async Task<IReadOnlyList<AdviserAvailability>> GetAvailabilityAsync(
        IReadOnlyList<string> adviserIds,
        LocationMeetingWindow window,
        CancellationToken ct)
    {
        if (adviserIds is null || adviserIds.Count == 0)
            return Array.Empty<AdviserAvailability>();

        var ids = adviserIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ids.Length == 0)
            return Array.Empty<AdviserAvailability>();

        return await _calendarClient.GetAdviserAvailabilityBatchAsync(ids, window, ct);
    }
}
