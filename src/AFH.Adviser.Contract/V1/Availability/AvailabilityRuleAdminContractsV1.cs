namespace AFH.Adviser.Contract.V1.Availability;

public sealed record AvailabilityRuleUpsertRequestV1(
    string? ProjectContext,
    string? AdviserId,
    string? AdviserName,
    string? DayOfWeek,
    string? StartTime,
    string? EndTime,
    int Capacity,
    string? EffectiveFrom,
    string? EffectiveTo,
    string? Status,
    string? Notes);

public sealed record AvailabilityRuleResponseV1(
    string Id,
    string AdviserId,
    string? AdviserName,
    string? DayOfWeek,
    string StartTime,
    string EndTime,
    int Capacity,
    string? EffectiveFrom,
    string? EffectiveTo,
    string Status,
    string? Notes);

public sealed record AvailabilityTimeSlotsResponseV1(IReadOnlyList<AvailabilityTimeSlotResponseV1> Slots);

public sealed record AvailabilityTimeSlotResponseV1(
    string Id,
    string AdviserId,
    string Date,
    string StartTime,
    string EndTime,
    bool IsBooked,
    string? BookingId,
    string Status);

public sealed record AvailabilityTimeSlotOverrideRequestV1(
    string? ProjectContext,
    string? AdviserId,
    string? AdviserName,
    string? Date,
    string? StartTime,
    string? EndTime,
    bool IsBooked,
    string? Status,
    string? OverrideType,
    string? Reason,
    int? Capacity);

public sealed record AvailabilityTimeSlotOverrideResponseV1(
    string Id,
    string AdviserId,
    string? AdviserName,
    string Date,
    string StartTime,
    string EndTime,
    bool IsBooked,
    string? BookingId,
    string Status,
    string? OverrideType,
    string? Reason);
