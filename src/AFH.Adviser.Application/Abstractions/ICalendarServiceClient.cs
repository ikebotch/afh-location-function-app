namespace AFH.Adviser.Application.Abstractions;

public interface ICalendarServiceClient
{
    Task<CalendarSubscriptionEnsureSummary> EnsureSubscriptionsAsync(
        IReadOnlyList<string> userIds,
        CancellationToken ct);
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
