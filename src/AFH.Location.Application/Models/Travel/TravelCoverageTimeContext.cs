using AFH.Location.Domain.Travel;

namespace AFH.Location.Application.Models.Travel;

public sealed record TravelCoverageTimeContext
{
    public DateTimeOffset? RequestedDepartureTime { get; init; }
    public TravelCoverageTimingMode TimingMode { get; init; } = TravelCoverageTimingMode.TimeIndependent;
    public TravelCoverageSlotResponseMode SlotResponseMode { get; init; } = TravelCoverageSlotResponseMode.Grouped;
    /// <summary>Window start for slot generation (inclusive).</summary>
    public DateTimeOffset? StartTime { get; init; }
    /// <summary>Window end for slot generation (exclusive).</summary>
    public DateTimeOffset? EndTime { get; init; }
    /// <summary>Duration of each slot in minutes. When omitted the full window is treated as a single slot.</summary>
    public int? SearchIntervalMinutes { get; init; }
}
