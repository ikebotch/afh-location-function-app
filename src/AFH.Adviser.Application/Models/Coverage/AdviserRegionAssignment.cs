namespace AFH.Adviser.Application.Models.Coverage;

public sealed record AdviserRegionAssignment(
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
