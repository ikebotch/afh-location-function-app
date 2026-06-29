using AFH.Adviser.Application.Abstractions.Availability;
using AFH.Adviser.Application.Models.Availability;
using AFH.Adviser.Infrastructure.Persistence.Availability.Entities;
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
                    Id = x.Id,
                    DayOfWeek = x.DayOfWeek,
                    Start = x.Start,
                    End = x.End,
                    IsActive = x.IsActive
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

    public async Task<AvailabilityRuleRecord> CreateRuleAsync(AvailabilityRuleUpsert request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var ruleSet = await GetOrCreateActiveRuleSetAsync(request.ProjectContext ?? "Booking", now, ct);

        var pattern = new AdviserWorkingPatternRuleEntity
        {
            RuleSetId = ruleSet.Id,
            AdviserId = request.AdviserId,
            DayOfWeek = request.DayOfWeek,
            Start = request.StartTime,
            End = request.EndTime,
            IsActive = IsActive(request.Status),
            CreatedUtc = now
        };

        _db.AdviserWorkingPatternRules.Add(pattern);
        await UpsertCapacityLimitAsync(ruleSet.Id, request, now, ct);
        ruleSet.UpdatedUtc = now;
        await _db.SaveChangesAsync(ct);

        return ToRecord(pattern, request);
    }

    public async Task<AvailabilityRuleRecord?> UpdateRuleAsync(string id, AvailabilityRuleUpsert request, CancellationToken ct)
    {
        var pattern = await FindPatternAsync(id, request.AdviserId, ct);
        if (pattern is null)
            return null;

        var now = DateTime.UtcNow;
        pattern.AdviserId = request.AdviserId;
        pattern.DayOfWeek = request.DayOfWeek;
        pattern.Start = request.StartTime;
        pattern.End = request.EndTime;
        pattern.IsActive = IsActive(request.Status);
        pattern.UpdatedUtc = now;

        await UpsertCapacityLimitAsync(pattern.RuleSetId, request, now, ct);
        await TouchRuleSetAsync(pattern.RuleSetId, now, ct);
        await _db.SaveChangesAsync(ct);

        return ToRecord(pattern, request);
    }

    public async Task<bool> DeleteRuleAsync(string id, CancellationToken ct)
    {
        var pattern = await FindPatternAsync(id, null, ct);
        if (pattern is null)
            return false;

        var now = DateTime.UtcNow;
        pattern.IsActive = false;
        pattern.UpdatedUtc = now;
        await DisableCapacityLimitAsync(pattern.RuleSetId, pattern.AdviserId, now, ct);
        await TouchRuleSetAsync(pattern.RuleSetId, now, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<AvailabilityRuleSetEntity> GetOrCreateActiveRuleSetAsync(string projectContext, DateTime now, CancellationToken ct)
    {
        var context = string.IsNullOrWhiteSpace(projectContext) ? "Booking" : projectContext.Trim();
        var ruleSet = await _db.AvailabilityRuleSets
            .Where(x => x.IsActive && x.ProjectContext == context)
            .OrderByDescending(x => x.UpdatedUtc ?? x.CreatedUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (ruleSet is not null)
            return ruleSet;

        ruleSet = new AvailabilityRuleSetEntity
        {
            Name = $"{context} availability rules",
            ProjectContext = context,
            IsActive = true,
            MinimumAppointmentMinutes = 30,
            DefaultWorkingDayStart = "08:00",
            DefaultWorkingDayEnd = "17:00",
            CapacityWindowDays = 14,
            CreatedUtc = now
        };

        _db.AvailabilityRuleSets.Add(ruleSet);
        await _db.SaveChangesAsync(ct);
        return ruleSet;
    }

    private async Task UpsertCapacityLimitAsync(int ruleSetId, AvailabilityRuleUpsert request, DateTime now, CancellationToken ct)
    {
        var limit = await _db.AdviserCapacityLimitRules
            .FirstOrDefaultAsync(x => x.RuleSetId == ruleSetId && x.AdviserId == request.AdviserId, ct);

        if (limit is null)
        {
            _db.AdviserCapacityLimitRules.Add(new AdviserCapacityLimitRuleEntity
            {
                RuleSetId = ruleSetId,
                AdviserId = request.AdviserId,
                MaxActiveBookings = request.Capacity,
                DailyLimit = request.Capacity > 0 ? request.Capacity : null,
                IsActive = IsActive(request.Status),
                CreatedUtc = now
            });
            return;
        }

        limit.MaxActiveBookings = request.Capacity;
        limit.DailyLimit = request.Capacity > 0 ? request.Capacity : null;
        limit.IsActive = IsActive(request.Status);
        limit.UpdatedUtc = now;
    }

    private async Task TouchRuleSetAsync(int ruleSetId, DateTime now, CancellationToken ct)
    {
        var ruleSet = await _db.AvailabilityRuleSets.FirstOrDefaultAsync(x => x.Id == ruleSetId, ct);
        if (ruleSet is not null)
            ruleSet.UpdatedUtc = now;
    }

    private async Task DisableCapacityLimitAsync(int ruleSetId, string adviserId, DateTime now, CancellationToken ct)
    {
        var limit = await _db.AdviserCapacityLimitRules
            .FirstOrDefaultAsync(x => x.RuleSetId == ruleSetId && x.AdviserId == adviserId, ct);
        if (limit is null)
            return;

        limit.IsActive = false;
        limit.UpdatedUtc = now;
    }

    private async Task<AdviserWorkingPatternRuleEntity?> FindPatternAsync(string id, string? adviserId, CancellationToken ct)
    {
        if (int.TryParse(id, out var numericId))
            return await _db.AdviserWorkingPatternRules.FirstOrDefaultAsync(x => x.Id == numericId, ct);

        if (!string.IsNullOrWhiteSpace(adviserId))
        {
            return await _db.AdviserWorkingPatternRules
                .OrderByDescending(x => x.UpdatedUtc ?? x.CreatedUtc)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(x => x.AdviserId == adviserId, ct);
        }

        return await _db.AdviserWorkingPatternRules
            .OrderByDescending(x => x.UpdatedUtc ?? x.CreatedUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(x => x.AdviserId == id, ct);
    }

    private static bool IsActive(string? status)
        => !string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase);

    private static AvailabilityRuleRecord ToRecord(AdviserWorkingPatternRuleEntity pattern, AvailabilityRuleUpsert request)
        => new()
        {
            Id = pattern.Id.ToString(),
            AdviserId = pattern.AdviserId,
            AdviserName = request.AdviserName,
            DayOfWeek = pattern.DayOfWeek ?? request.DayOfWeek,
            StartTime = pattern.Start,
            EndTime = pattern.End,
            Capacity = request.Capacity,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = pattern.IsActive ? "Active" : "Inactive",
            Notes = request.Notes
        };
}
