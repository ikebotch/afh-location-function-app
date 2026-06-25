using AFH.Adviser.Application.Abstractions.Availability;
using AFH.Adviser.Application.Models.Availability;
using AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;
using Microsoft.EntityFrameworkCore;

namespace AFH.Adviser.Infrastructure.Persistence.Availability;

public sealed class SqlAdviserAvailabilityRulesRepository : IAdviserAvailabilityRulesRepository
{
    private readonly AdviserDirectoryDbContext _db;

    public SqlAdviserAvailabilityRulesRepository(AdviserDirectoryDbContext db)
    {
        _db = db;
    }

    public async Task<AdviserAvailabilityRules?> GetActiveRulesAsync(string projectContext, CancellationToken ct)
    {
        var context = string.IsNullOrWhiteSpace(projectContext) ? "Booking" : projectContext.Trim();

        var ruleSet = await _db.AvailabilityRuleSets
            .AsNoTracking()
            .Include(x => x.WorkingPatterns)
            .Include(x => x.CapacityLimits)
            .Where(x => x.IsActive && x.ProjectContext == context)
            .OrderByDescending(x => x.UpdatedUtc ?? x.CreatedUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (ruleSet is null)
            return null;

        return new AdviserAvailabilityRules
        {
            MinimumAppointmentMinutes = ruleSet.MinimumAppointmentMinutes,
            DefaultWorkingDayStart = ruleSet.DefaultWorkingDayStart,
            DefaultWorkingDayEnd = ruleSet.DefaultWorkingDayEnd,
            CapacityWindowDays = ruleSet.CapacityWindowDays,
            WorkingPatterns = ruleSet.WorkingPatterns
                .Where(x => x.IsActive)
                .Select(x => new AdviserWorkingPatternRule
                {
                    AdviserId = x.AdviserId,
                    Start = x.Start,
                    End = x.End
                })
                .ToArray(),
            CapacityLimits = ruleSet.CapacityLimits
                .Where(x => x.IsActive)
                .Select(x => new AdviserCapacityLimitRule
                {
                    AdviserId = x.AdviserId,
                    MaxActiveBookings = x.MaxActiveBookings,
                    DailyLimit = x.DailyLimit,
                    WeeklyLimit = x.WeeklyLimit,
                    MonthlyLimit = x.MonthlyLimit
                })
                .ToArray()
        };
    }
}
