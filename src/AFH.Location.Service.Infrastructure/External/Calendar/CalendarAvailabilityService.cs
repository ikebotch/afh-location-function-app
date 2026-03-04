using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Contracts.V1.Requests;

namespace AFH.Location.Service.Infrastructure.External.Calendar;

public sealed class CalendarAvailabilityService : ICalendarAvailabilityService
{
    private readonly ICalendarServiceClient _calendarClient;

    public CalendarAvailabilityService(ICalendarServiceClient calendarClient)
    {
        _calendarClient = calendarClient;
    }

    public async Task<IReadOnlyList<AdviserAvailability>> GetAvailabilityAsync(
        IReadOnlyList<string> adviserIds,
        MeetingWindow window,
        CancellationToken ct)
    {
        var tasks = adviserIds.Select(adviserId => _calendarClient.GetAdviserAvailabilityAsync(adviserId, window, ct));
        return await Task.WhenAll(tasks);
    }
}
