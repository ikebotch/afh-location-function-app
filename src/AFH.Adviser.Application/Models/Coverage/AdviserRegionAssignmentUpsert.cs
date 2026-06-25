namespace AFH.Adviser.Application.Models.Coverage;

public sealed record AdviserRegionAssignmentUpsert(
    Guid RegionId,
    string AdviserId,
    string AdviserName,
    string? Role,
    bool IsLead,
    bool IsActive);
