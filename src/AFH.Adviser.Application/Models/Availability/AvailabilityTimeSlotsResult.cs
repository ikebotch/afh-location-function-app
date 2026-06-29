namespace AFH.Adviser.Application.Models.Availability;

public sealed class AvailabilityTimeSlotsResult
{
    private AvailabilityTimeSlotsResult(bool succeeded, IReadOnlyList<AvailabilityTimeSlot> slots, string? errorCode, string? errorMessage)
    {
        Succeeded = succeeded;
        Slots = slots;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }
    public IReadOnlyList<AvailabilityTimeSlot> Slots { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    public static AvailabilityTimeSlotsResult Success(IReadOnlyList<AvailabilityTimeSlot> slots)
        => new(true, slots, null, null);

    public static AvailabilityTimeSlotsResult Failure(string errorCode, string errorMessage)
        => new(false, [], errorCode, errorMessage);
}
