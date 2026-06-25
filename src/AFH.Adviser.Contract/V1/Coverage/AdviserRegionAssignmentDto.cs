namespace AFH.Adviser.Contract.V1.Coverage;

public sealed record AdviserRegionAssignmentDto(
    Guid Id,
    Guid RegionId,
    string RegionCode,
    string AdviserId,
    string AdviserName,
    string? Role,
    bool IsLead,
    bool IsActive,
    DateTime CreatedUtc,
    DateTime? UpdatedUtc);
