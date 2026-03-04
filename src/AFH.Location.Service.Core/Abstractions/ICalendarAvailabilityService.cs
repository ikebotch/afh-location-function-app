using AFH.Location.Service.Core.Contracts.V1.Requests;

namespace AFH.Location.Service.Core.Abstractions;

public interface ICalendarAvailabilityService
{
    Task<IReadOnlyList<AdviserAvailability>> GetAvailabilityAsync(
        IReadOnlyList<string> adviserIds,
        MeetingWindow window,
        CancellationToken ct);
}

public sealed class AdviserAvailability
{
    public string AdviserId { get; init; } = default!;
    public IReadOnlyList<BusyBlock> BusyBlocks { get; init; } = Array.Empty<BusyBlock>();
    public bool IsOutOfOffice { get; init; }
    public string? CurrentLocationPostcode { get; init; }
}

public sealed class BusyBlock
{
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
}
