using AFH.Location.Application.Abstractions.Advisers;
using AFH.Location.Application.Abstractions.Calendar;
using AFH.Location.Application.Admin;
using AFH.Location.Domain.Entities;
using AFH.Location.Infrastructure.External.Calendar;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AFH.Location.Tests;

public sealed class AdviserCacheSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_UpsertsCacheAndEnsuresCalendarSubscriptionsForMailboxIds()
    {
        var source = new Mock<IAdviserSourceRepository>(MockBehavior.Strict);
        source.Setup(x => x.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Adviser
                {
                    AdviserId = "adv-1",
                    DisplayName = "Adviser One",
                    MailboxUserId = "a@tenant.com",
                    HomePostcode = "B1 1AA",
                    Region = "West Midlands",
                    IsActive = true
                },
                new Adviser
                {
                    AdviserId = "adv-2",
                    DisplayName = "Adviser Two",
                    MailboxUserId = "A@tenant.com",
                    HomePostcode = "B2 2BB",
                    Region = "West Midlands",
                    IsActive = true
                },
                new Adviser
                {
                    AdviserId = "adv-3",
                    DisplayName = "Adviser Three",
                    MailboxUserId = "b@tenant.com",
                    HomePostcode = "B3 3CC",
                    Region = "West Midlands",
                    IsActive = true
                }
            ]);

        var cache = new Mock<IAdviserReferenceCacheRepository>(MockBehavior.Strict);
        cache.Setup(x => x.UpsertAsync(It.Is<IReadOnlyCollection<Adviser>>(r => r.Count == 3), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var calendar = new Mock<ICalendarServiceClient>(MockBehavior.Strict);
        calendar.Setup(x => x.EnsureSubscriptionsAsync(
                It.Is<IReadOnlyList<string>>(ids =>
                    ids.Count == 2 &&
                    ids.Contains("a@tenant.com") &&
                    ids.Contains("b@tenant.com")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CalendarSubscriptionEnsureSummary
            {
                RequestedCount = 2,
                ProcessedCount = 2
            });

        var sut = new AdviserCacheSyncService(
            source.Object,
            cache.Object,
            calendar.Object,
            NullLogger<AdviserCacheSyncService>.Instance);

        var synced = await sut.SyncAsync(null, CancellationToken.None);

        Assert.Equal(3, synced);
        source.VerifyAll();
        cache.VerifyAll();
        calendar.VerifyAll();
    }
}
