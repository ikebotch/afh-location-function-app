using AFH.Adviser.Application.Abstractions.Availability;
using AFH.Adviser.Application.Models.Availability;

namespace AFH.Adviser.Application.Services.Availability;

public sealed class AdviserAvailabilityRulesService : IAdviserAvailabilityRulesService
{
    private readonly IAdviserAvailabilityRulesRepository _repository;

    public AdviserAvailabilityRulesService(IAdviserAvailabilityRulesRepository repository)
    {
        _repository = repository;
    }

    public Task<AdviserAvailabilityRules?> GetActiveRulesAsync(string? projectContext, CancellationToken ct)
    {
        var context = string.IsNullOrWhiteSpace(projectContext) ? "Booking" : projectContext.Trim();
        return _repository.GetActiveRulesAsync(context, ct);
    }

    public async Task<AdviserAvailabilityRules> GetAdminRulesAsync(string? projectContext, string? adviserId, CancellationToken ct)
    {
        var rules = await GetActiveRulesAsync(projectContext, ct);
        return rules is null ? new AdviserAvailabilityRules() : FilterRulesForAdviser(rules, adviserId);
    }

    public async Task<AvailabilityTimeSlotsResult> GetAdminTimeSlotsAsync(AvailabilityTimeSlotsQuery query, CancellationToken ct)
    {
        var rules = await GetActiveRulesAsync(query.ProjectContext, ct);
        if (rules is null)
            return AvailabilityTimeSlotsResult.Success([]);

        var from = query.From ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var to = query.To ?? from.AddDays(Math.Max(0, rules.CapacityWindowDays - 1));
        if (to < from)
            return AvailabilityTimeSlotsResult.Failure("INVALID_DATE_RANGE", "to must be on or after from.");

        return AvailabilityTimeSlotsResult.Success(GenerateSlots(rules, query.AdviserId, from, to));
    }

    public Task<AvailabilityRuleRecord> CreateRuleAsync(AvailabilityRuleUpsert request, CancellationToken ct)
    {
        ValidateRule(request);
        return _repository.CreateRuleAsync(NormalizeRule(request), ct);
    }

    public Task<AvailabilityRuleRecord?> UpdateRuleAsync(string id, AvailabilityRuleUpsert request, CancellationToken ct)
    {
        ValidateRule(request);
        return _repository.UpdateRuleAsync(id, NormalizeRule(request), ct);
    }

    public Task<bool> DeleteRuleAsync(string id, CancellationToken ct)
        => string.IsNullOrWhiteSpace(id) ? Task.FromResult(false) : _repository.DeleteRuleAsync(id, ct);

    private static AdviserAvailabilityRules FilterRulesForAdviser(AdviserAvailabilityRules rules, string? adviserId)
    {
        if (string.IsNullOrWhiteSpace(adviserId))
            return WithValidWorkingPatterns(rules, rules.WorkingPatterns);

        return new AdviserAvailabilityRules
        {
            MinimumAppointmentMinutes = rules.MinimumAppointmentMinutes,
            DefaultWorkingDayStart = rules.DefaultWorkingDayStart,
            DefaultWorkingDayEnd = rules.DefaultWorkingDayEnd,
            CapacityWindowDays = rules.CapacityWindowDays,
            WorkingPatterns = rules.WorkingPatterns
                .Where(rule => HasDay(rule) && string.Equals(rule.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase))
                .ToArray(),
            CapacityLimits = rules.CapacityLimits
                .Where(rule => string.Equals(rule.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase))
                .ToArray()
        };
    }

    private static AdviserAvailabilityRules WithValidWorkingPatterns(
        AdviserAvailabilityRules rules,
        IReadOnlyCollection<AdviserWorkingPatternRule> workingPatterns)
        => new()
        {
            MinimumAppointmentMinutes = rules.MinimumAppointmentMinutes,
            DefaultWorkingDayStart = rules.DefaultWorkingDayStart,
            DefaultWorkingDayEnd = rules.DefaultWorkingDayEnd,
            CapacityWindowDays = rules.CapacityWindowDays,
            WorkingPatterns = workingPatterns.Where(HasDay).ToArray(),
            CapacityLimits = rules.CapacityLimits
        };

    private static bool HasDay(AdviserWorkingPatternRule pattern)
        => !string.IsNullOrWhiteSpace(pattern.DayOfWeek);

    private static IReadOnlyList<AvailabilityTimeSlot> GenerateSlots(
        AdviserAvailabilityRules rules,
        string? adviserId,
        DateOnly from,
        DateOnly to)
    {
        var filtered = FilterRulesForAdviser(rules, adviserId);
        if (filtered.WorkingPatterns.Count == 0)
            return [];

        var duration = Math.Max(1, filtered.MinimumAppointmentMinutes);
        var slots = new List<AvailabilityTimeSlot>();

        foreach (var pattern in filtered.WorkingPatterns)
        {
            if (!TimeOnly.TryParse(pattern.Start, out var start) || !TimeOnly.TryParse(pattern.End, out var end) || end <= start)
                continue;

            for (var date = from; date <= to; date = date.AddDays(1))
            {
                if (!AppliesToDate(pattern, date) || !IsEffectiveOn(pattern, date))
                    continue;

                for (var slotStart = start; slotStart.AddMinutes(duration) <= end; slotStart = slotStart.AddMinutes(duration))
                {
                    var slotEnd = slotStart.AddMinutes(duration);
                    slots.Add(new AvailabilityTimeSlot
                    {
                        Id = $"{pattern.AdviserId}:{date:yyyyMMdd}:{slotStart:HHmm}",
                        AdviserId = pattern.AdviserId,
                        Date = date.ToString("yyyy-MM-dd"),
                        StartTime = slotStart.ToString("HH:mm"),
                        EndTime = slotEnd.ToString("HH:mm"),
                        Status = "Available"
                    });
                }
            }
        }

        return slots
            .OrderBy(slot => slot.Date, StringComparer.Ordinal)
            .ThenBy(slot => slot.StartTime, StringComparer.Ordinal)
            .ThenBy(slot => slot.AdviserId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool AppliesToDate(AdviserWorkingPatternRule pattern, DateOnly date)
        => string.Equals(pattern.DayOfWeek, date.DayOfWeek.ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool IsEffectiveOn(AdviserWorkingPatternRule pattern, DateOnly date)
    {
        if (DateOnly.TryParse(pattern.EffectiveFrom, out var effectiveFrom) && date < effectiveFrom)
            return false;

        if (DateOnly.TryParse(pattern.EffectiveTo, out var effectiveTo) && date > effectiveTo)
            return false;

        return true;
    }

    private static void ValidateRule(AvailabilityRuleUpsert request)
    {
        if (string.IsNullOrWhiteSpace(request.AdviserId))
            throw new ArgumentException("adviserId is required.", nameof(request));
        if (!TimeOnly.TryParse(request.StartTime, out var start))
            throw new ArgumentException("startTime must be a valid time.", nameof(request));
        if (!TimeOnly.TryParse(request.EndTime, out var end))
            throw new ArgumentException("endTime must be a valid time.", nameof(request));
        if (end <= start)
            throw new ArgumentException("endTime must be after startTime.", nameof(request));
        if (request.Capacity < 0)
            throw new ArgumentException("capacity cannot be negative.", nameof(request));
        if (!string.IsNullOrWhiteSpace(request.EffectiveFrom) && !DateOnly.TryParse(request.EffectiveFrom, out _))
            throw new ArgumentException("effectiveFrom must be a valid date.", nameof(request));
        if (!string.IsNullOrWhiteSpace(request.EffectiveTo) && !DateOnly.TryParse(request.EffectiveTo, out _))
            throw new ArgumentException("effectiveTo must be a valid date.", nameof(request));
        if (DateOnly.TryParse(request.EffectiveFrom, out var effectiveFrom)
            && DateOnly.TryParse(request.EffectiveTo, out var effectiveTo)
            && effectiveTo < effectiveFrom)
        {
            throw new ArgumentException("effectiveTo must be on or after effectiveFrom.", nameof(request));
        }
    }

    private static string? NormalizeDate(string? date)
        => DateOnly.TryParse(date, out var parsed) ? parsed.ToString("yyyy-MM-dd") : null;

    private static AvailabilityRuleUpsert NormalizeRule(AvailabilityRuleUpsert request)
        => new()
        {
            ProjectContext = string.IsNullOrWhiteSpace(request.ProjectContext) ? "Booking" : request.ProjectContext.Trim(),
            AdviserId = request.AdviserId.Trim(),
            AdviserName = request.AdviserName,
            DayOfWeek = string.IsNullOrWhiteSpace(request.DayOfWeek) ? "Monday" : request.DayOfWeek,
            StartTime = TimeOnly.Parse(request.StartTime).ToString("HH:mm"),
            EndTime = TimeOnly.Parse(request.EndTime).ToString("HH:mm"),
            Capacity = request.Capacity,
            EffectiveFrom = NormalizeDate(request.EffectiveFrom),
            EffectiveTo = NormalizeDate(request.EffectiveTo),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status,
            Notes = request.Notes
        };
}
