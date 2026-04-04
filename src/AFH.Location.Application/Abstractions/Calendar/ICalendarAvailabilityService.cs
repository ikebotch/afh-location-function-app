using AFH.Location.Application.Models.V1;

namespace AFH.Location.Application.Abstractions;

public interface ICalendarAvailabilityService
{
    Task<IReadOnlyList<AdviserAvailability>> GetAvailabilityAsync(
        IReadOnlyList<string> adviserIds,
        LocationMeetingWindow window,
        CancellationToken ct);
}

public sealed class AdviserAvailability
{
    public string AdviserId { get; init; } = default!;
    public IReadOnlyList<BusyBlock> BusyBlocks { get; init; } = Array.Empty<BusyBlock>();
    public bool IsOutOfOffice { get; init; }
    public string? CurrentLocationPostcode { get; init; }
    public CalendarAvailabilityState State { get; init; } = CalendarAvailabilityState.Ok;
    public string? StateMessage { get; init; }
}

public sealed class BusyBlock
{
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string? LocationPostcode { get; init; }
}

public enum CalendarAvailabilityState
{
    Ok = 0,
    MailboxNotFound = 1,
    ServiceUnavailable = 2,
    ConfigurationMissing = 3,
    UnknownError = 4
}
