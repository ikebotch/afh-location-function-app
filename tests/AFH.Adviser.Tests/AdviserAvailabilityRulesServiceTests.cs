using AFH.Adviser.Application.Abstractions.Availability;
using AFH.Adviser.Application.Models.Availability;
using AFH.Adviser.Application.Services.Availability;

namespace AFH.Adviser.Tests;

public sealed class AdviserAvailabilityRulesServiceTests
{
    [Fact]
    public async Task GetActiveRulesAsync_ReturnsNull_WhenNoActiveRulesExist()
    {
        var sut = new AdviserAvailabilityRulesService(new StubRulesRepository(null));

        var result = await sut.GetActiveRulesAsync("Booking", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAdminRulesAsync_ReturnsEmptyRules_WhenNoActiveRulesExist()
    {
        var sut = new AdviserAvailabilityRulesService(new StubRulesRepository(null));

        var result = await sut.GetAdminRulesAsync("Booking", null, CancellationToken.None);

        Assert.Empty(result.WorkingPatterns);
        Assert.Empty(result.CapacityLimits);
    }

    [Fact]
    public async Task GetAdminRulesAsync_FiltersRulesForRequestedAdviser()
    {
        var sut = new AdviserAvailabilityRulesService(new StubRulesRepository(CreateRules()));

        var result = await sut.GetAdminRulesAsync("Booking", "adv-002", CancellationToken.None);

        var pattern = Assert.Single(result.WorkingPatterns);
        Assert.Equal("adv-002", pattern.AdviserId);
        var capacity = Assert.Single(result.CapacityLimits);
        Assert.Equal("adv-002", capacity.AdviserId);
    }

    [Fact]
    public async Task GetAdminTimeSlotsAsync_ReturnsEmptySlots_WhenNoActiveRulesExist()
    {
        var sut = new AdviserAvailabilityRulesService(new StubRulesRepository(null));

        var result = await sut.GetAdminTimeSlotsAsync(new AvailabilityTimeSlotsQuery(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Slots);
    }

    [Fact]
    public async Task GetAdminTimeSlotsAsync_RejectsInvalidDateRange()
    {
        var sut = new AdviserAvailabilityRulesService(new StubRulesRepository(CreateRules()));

        var result = await sut.GetAdminTimeSlotsAsync(new AvailabilityTimeSlotsQuery
        {
            From = new DateOnly(2026, 6, 30),
            To = new DateOnly(2026, 6, 29)
        }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_DATE_RANGE", result.ErrorCode);
    }

    [Fact]
    public async Task GetAdminTimeSlotsAsync_GeneratesSlotsFromWorkingPatterns()
    {
        var sut = new AdviserAvailabilityRulesService(new StubRulesRepository(CreateRules()));

        var result = await sut.GetAdminTimeSlotsAsync(new AvailabilityTimeSlotsQuery
        {
            AdviserId = "adv-002",
            From = new DateOnly(2026, 6, 29),
            To = new DateOnly(2026, 6, 29)
        }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Slots.Count);
        Assert.Collection(
            result.Slots,
            slot =>
            {
                Assert.Equal("adv-002", slot.AdviserId);
                Assert.Equal("09:30", slot.StartTime);
                Assert.Equal("10:00", slot.EndTime);
            },
            slot =>
            {
                Assert.Equal("adv-002", slot.AdviserId);
                Assert.Equal("10:00", slot.StartTime);
                Assert.Equal("10:30", slot.EndTime);
            });
    }

    [Fact]
    public async Task GetAdminTimeSlotsAsync_OnlyGeneratesSlotsForConfiguredDay()
    {
        var sut = new AdviserAvailabilityRulesService(new StubRulesRepository(new AdviserAvailabilityRules
        {
            MinimumAppointmentMinutes = 30,
            WorkingPatterns =
            [
                new AdviserWorkingPatternRule
                {
                    AdviserId = "adv-002",
                    DayOfWeek = "Monday",
                    Start = "09:00",
                    End = "10:00"
                }
            ]
        }));

        var result = await sut.GetAdminTimeSlotsAsync(new AvailabilityTimeSlotsQuery
        {
            AdviserId = "adv-002",
            From = new DateOnly(2026, 6, 29),
            To = new DateOnly(2026, 6, 30)
        }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Slots.Count);
        Assert.All(result.Slots, slot => Assert.Equal("2026-06-29", slot.Date));
    }

    private static AdviserAvailabilityRules CreateRules()
        => new()
        {
            MinimumAppointmentMinutes = 30,
            CapacityWindowDays = 2,
            WorkingPatterns =
            [
                new AdviserWorkingPatternRule { AdviserId = "adv-001", Start = "08:00", End = "08:30" },
                new AdviserWorkingPatternRule { AdviserId = "adv-002", Start = "09:30", End = "10:30" }
            ],
            CapacityLimits =
            [
                new AdviserCapacityLimitRule { AdviserId = "adv-001", MaxActiveBookings = 1 },
                new AdviserCapacityLimitRule { AdviserId = "adv-002", MaxActiveBookings = 2 }
            ]
        };

    private sealed class StubRulesRepository : IAdviserAvailabilityRulesRepository
    {
        private readonly AdviserAvailabilityRules? _rules;

        public StubRulesRepository(AdviserAvailabilityRules? rules)
        {
            _rules = rules;
        }

        public Task<AdviserAvailabilityRules?> GetActiveRulesAsync(string projectContext, CancellationToken ct)
            => Task.FromResult(_rules);

        public Task<AvailabilityRuleRecord> CreateRuleAsync(AvailabilityRuleUpsert request, CancellationToken ct)
            => Task.FromResult(new AvailabilityRuleRecord());

        public Task<AvailabilityRuleRecord?> UpdateRuleAsync(string id, AvailabilityRuleUpsert request, CancellationToken ct)
            => Task.FromResult<AvailabilityRuleRecord?>(new AvailabilityRuleRecord { Id = id });

        public Task<bool> DeleteRuleAsync(string id, CancellationToken ct)
            => Task.FromResult(true);
    }
}
