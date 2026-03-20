using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Contracts.V1.Requests;

namespace AFH.Location.Service.Infrastructure.External.Calendar;

public interface ICalendarServiceClient
{
    Task<AdviserAvailability> GetAdviserAvailabilityAsync(
        string adviserId,
        MeetingWindow window,
        CancellationToken ct);

    Task<IReadOnlyList<AdviserAvailability>> GetAdviserAvailabilityBatchAsync(
        IReadOnlyList<string> adviserIds,
        MeetingWindow window,
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
