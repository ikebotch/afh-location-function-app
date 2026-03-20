using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Contracts.V1.Requests;
using Microsoft.Extensions.Caching.Memory;

namespace AFH.Location.Service.Infrastructure.External.Calendar;

public sealed class CalendarAvailabilityService : ICalendarAvailabilityService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);
    private readonly ICalendarServiceClient _calendarClient;
    private readonly IMemoryCache _cache;

    public CalendarAvailabilityService(
        ICalendarServiceClient calendarClient,
        IMemoryCache cache)
    {
        _calendarClient = calendarClient;
        _cache = cache;
    }

    public async Task<IReadOnlyList<AdviserAvailability>> GetAvailabilityAsync(
        IReadOnlyList<string> adviserIds,
        MeetingWindow window,
        CancellationToken ct)
    {
        if (adviserIds is null || adviserIds.Count == 0)
            return Array.Empty<AdviserAvailability>();

        var orderedIds = adviserIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var byId = new Dictionary<string, AdviserAvailability>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();

        foreach (var adviserId in orderedIds)
        {
            var key = BuildCacheKey(adviserId, window);
            if (_cache.TryGetValue<AdviserAvailability>(key, out var cached) && cached is not null)
            {
                byId[adviserId] = cached;
                continue;
            }

            missing.Add(adviserId);
        }

        if (missing.Count > 0)
        {
            var tasks = missing
                .Select(async adviserId =>
                {
                    var availability = await _calendarClient.GetAdviserAvailabilityAsync(adviserId, window, ct);
                    var key = BuildCacheKey(adviserId, window);
                    _cache.Set(key, availability, CacheTtl);
                    return availability;
                });

            var fetched = await Task.WhenAll(tasks);
            foreach (var item in fetched)
                byId[item.AdviserId] = item;
        }

        return orderedIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToArray();
    }

    private static string BuildCacheKey(string adviserId, MeetingWindow window)
    {
        // Round to minute to improve cache hits for equivalent windows.
        var requestedStartUtc = DateTime.SpecifyKind(window.RequestedStartUtc, DateTimeKind.Utc);
        var normalizedStart = new DateTime(
            requestedStartUtc.Year,
            requestedStartUtc.Month,
            requestedStartUtc.Day,
            requestedStartUtc.Hour,
            requestedStartUtc.Minute,
            0,
            DateTimeKind.Utc);

        return
            $"calendar:availability:{adviserId.ToLowerInvariant()}:{normalizedStart:O}:{window.DurationMinutes}:{window.SearchHorizonMinutes}";
    }
}
