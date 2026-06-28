
using AFH.Adviser.Application.Abstractions.Sync;
using AFH.Adviser.Application.Abstractions.Repositories;
using AFH.Adviser.Application.Abstractions.Clients;
using Microsoft.Extensions.Logging;

namespace AFH.Adviser.Application.Services.Sync;

public sealed class AdviserCacheSyncService : IAdviserCacheSyncService
{
    private readonly IAdviserSourceRepository _sourceRepository;
    private readonly IAdviserReferenceCacheRepository _cacheRepository;
    private readonly ICalendarServiceClient _calendarServiceClient;
    private readonly ILogger<AdviserCacheSyncService> _logger;

    public AdviserCacheSyncService(
        IAdviserSourceRepository sourceRepository,
        IAdviserReferenceCacheRepository cacheRepository,
        ICalendarServiceClient calendarServiceClient,
        ILogger<AdviserCacheSyncService> logger)
    {
        _sourceRepository = sourceRepository;
        _cacheRepository = cacheRepository;
        _calendarServiceClient = calendarServiceClient;
        _logger = logger;
    }

    public async Task<AdviserReferenceCacheSyncResult> SyncAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct)
    {
        var advisers = await _sourceRepository.GetAllAsync(adviserIds, ct);
        var syncedUtc = DateTime.UtcNow;
        var result = await _cacheRepository.UpsertAsync(advisers, syncedUtc, ct);

        var mailboxUserIds = advisers
            .Where(x => !string.IsNullOrWhiteSpace(x.MailboxUserId))
            .Select(x => x.MailboxUserId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (mailboxUserIds.Length > 0)
        {
            try
            {
                await _calendarServiceClient.EnsureSubscriptionsAsync(mailboxUserIds, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Calendar subscription ensure failed after adviser cache sync for {MailboxCount} mailbox user ids.", mailboxUserIds.Length);
            }
        }

        return result;
    }
}
