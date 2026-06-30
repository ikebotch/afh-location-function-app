using AFH.Adviser.Application.Models.Availability;
using AFH.Adviser.Contract.V1.Availability;

namespace AFH.Location.Function.Mapping.V1.Adviser;

public static class AdviserAvailabilityRulesContractMapper
{
    public static AdviserAvailabilityRulesResponseV1 ToContractResponse(AdviserAvailabilityRules rules)
        => new(
            rules.MinimumAppointmentMinutes,
            rules.DefaultWorkingDayStart,
            rules.DefaultWorkingDayEnd,
            rules.CapacityWindowDays,
            rules.WorkingPatterns.Select(x => new AdviserWorkingPatternRuleResponseV1(
                x.Id,
                x.AdviserId,
                x.DayOfWeek,
                x.Start,
                x.End,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.IsActive)).ToArray(),
            rules.CapacityLimits
                .Select(x => new AdviserCapacityLimitRuleResponseV1(
                    x.AdviserId,
                    x.MaxActiveBookings,
                    x.DailyLimit,
                    x.WeeklyLimit,
                    x.MonthlyLimit))
                .ToArray());
}
