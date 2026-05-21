using AFH.Location.Application.Calendar;

namespace AFH.Location.Application.Abstractions.Calendar;

public interface ICalendarServiceClient
{
    Task<AdviserAvailability> GetAdviserAvailabilityAsync(
        string adviserId,
        LocationMeetingWindow window,
        CancellationToken ct);

    Task<IReadOnlyList<AdviserAvailability>> GetAdviserAvailabilityBatchAsync(
        IReadOnlyList<string> adviserIds,
        LocationMeetingWindow window,
        CancellationToken ct);

    Task<CalendarSubscriptionEnsureSummary> EnsureSubscriptionsAsync(
        IReadOnlyList<string> userIds,
        CancellationToken ct);

    Task<CalendarAppointmentResult> CreateAppointmentAsync(
        CreateCalendarAppointmentRequest request,
        CancellationToken ct);

    Task UpdateAppointmentAsync(
        UpdateCalendarAppointmentRequest request,
        CancellationToken ct);

    Task CancelAppointmentAsync(
        CancelCalendarAppointmentRequest request,
        CancellationToken ct);
}

public sealed class CreateCalendarAppointmentRequest
{
    public string UserId { get; init; } = string.Empty;
    public string BookingId { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string Timezone { get; init; } = "UTC";
    public string? Body { get; init; }
}

public sealed class UpdateCalendarAppointmentRequest
{
    public string AppointmentId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string Timezone { get; init; } = "UTC";
    public string? Body { get; init; }
}

public sealed class CancelCalendarAppointmentRequest
{
    public string AppointmentId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
}

public sealed class CalendarAppointmentResult
{
    public string AppointmentId { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
}

public sealed class CalendarSubscriptionEnsureSummary
{
    public int RequestedCount { get; init; }
    public int ProcessedCount { get; init; }
    public int CreatedCount { get; init; }
    public int RenewedCount { get; init; }
    public int AlreadyActiveCount { get; init; }
    public int InvalidUserCount { get; init; }
    public int UnavailableMailboxCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<CalendarSubscriptionEnsureItem> Results { get; init; } = Array.Empty<CalendarSubscriptionEnsureItem>();
}

public sealed class CalendarSubscriptionEnsureItem
{
    public string UserId { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string? SubscriptionId { get; init; }
    public DateTime? ExpirationUtc { get; init; }
    public string? Message { get; init; }
}
