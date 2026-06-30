using AFH.Adviser.Infrastructure.Persistence.Availability;
using AFH.Adviser.Infrastructure.Persistence.Availability.Entities;
using AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;
using Microsoft.EntityFrameworkCore;

namespace AFH.Adviser.Tests;

public sealed class AdviserAvailabilityRulesRepositoryTests
{
    private static readonly DateTime FixedNow = new(2026, 06, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetActiveRulesAsync_ReturnsActiveRulesForRequestedProjectContext()
    {
        await using var db = CreateDb();
        db.AvailabilityRuleSets.Add(new AvailabilityRuleSetEntity
        {
            Name = "inactive",
            ProjectContext = "Booking",
            IsActive = false,
            MinimumAppointmentMinutes = 15,
            DefaultWorkingDayStart = "07:00",
            DefaultWorkingDayEnd = "18:00",
            CapacityWindowDays = 1,
            CreatedUtc = FixedNow
        });
        db.AvailabilityRuleSets.Add(new AvailabilityRuleSetEntity
        {
            Name = "booking-default",
            ProjectContext = "Booking",
            IsActive = true,
            MinimumAppointmentMinutes = 45,
            DefaultWorkingDayStart = "09:00",
            DefaultWorkingDayEnd = "17:30",
            CapacityWindowDays = 2,
            CreatedUtc = FixedNow,
            WorkingPatterns =
            [
                new AdviserWorkingPatternRuleEntity
                {
                    AdviserId = "adv-1",
                    Start = "10:00",
                    End = "16:00",
                    EffectiveFrom = "2026-07-01",
                    EffectiveTo = "2026-07-31",
                    IsActive = true,
                    CreatedUtc = FixedNow
                },
                new AdviserWorkingPatternRuleEntity
                {
                    AdviserId = "disabled",
                    Start = "08:00",
                    End = "12:00",
                    IsActive = false,
                    CreatedUtc = FixedNow
                }
            ],
            CapacityLimits =
            [
                new AdviserCapacityLimitRuleEntity
                {
                    AdviserId = "adv-1",
                    MaxActiveBookings = 3,
                    DailyLimit = 3,
                    WeeklyLimit = 12,
                    MonthlyLimit = 40,
                    IsActive = true,
                    CreatedUtc = FixedNow
                },
                new AdviserCapacityLimitRuleEntity
                {
                    AdviserId = "disabled",
                    MaxActiveBookings = 1,
                    IsActive = false,
                    CreatedUtc = FixedNow
                }
            ]
        });
        db.AvailabilityRuleSets.Add(new AvailabilityRuleSetEntity
        {
            Name = "project-x",
            ProjectContext = "ProjectX",
            IsActive = true,
            MinimumAppointmentMinutes = 120,
            DefaultWorkingDayStart = "12:00",
            DefaultWorkingDayEnd = "20:00",
            CapacityWindowDays = 7,
            CreatedUtc = FixedNow
        });
        await db.SaveChangesAsync();

        var sut = new SqlAdviserAvailabilityRulesRepository(db);

        var rules = await sut.GetActiveRulesAsync("Booking", CancellationToken.None);

        Assert.NotNull(rules);
        Assert.Equal(45, rules!.MinimumAppointmentMinutes);
        Assert.Equal("09:00", rules.DefaultWorkingDayStart);
        Assert.Equal("17:30", rules.DefaultWorkingDayEnd);
        Assert.Equal(2, rules.CapacityWindowDays);

        var pattern = Assert.Single(rules.WorkingPatterns);
        Assert.Equal("adv-1", pattern.AdviserId);
        Assert.Equal("10:00", pattern.Start);
        Assert.Equal("16:00", pattern.End);
        Assert.Equal("2026-07-01", pattern.EffectiveFrom);
        Assert.Equal("2026-07-31", pattern.EffectiveTo);

        var capacity = Assert.Single(rules.CapacityLimits);
        Assert.Equal("adv-1", capacity.AdviserId);
        Assert.Equal(3, capacity.MaxActiveBookings);
        Assert.Equal(3, capacity.DailyLimit);
        Assert.Equal(12, capacity.WeeklyLimit);
        Assert.Equal(40, capacity.MonthlyLimit);

        var projectRules = await sut.GetActiveRulesAsync("ProjectX", CancellationToken.None);
        Assert.NotNull(projectRules);
        Assert.Equal(120, projectRules!.MinimumAppointmentMinutes);
        Assert.Equal("12:00", projectRules.DefaultWorkingDayStart);
        Assert.Equal("20:00", projectRules.DefaultWorkingDayEnd);
        Assert.Equal(7, projectRules.CapacityWindowDays);
    }

    private static AdviserDirectoryDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AdviserDirectoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AdviserDirectoryDbContext(options);
    }
}
