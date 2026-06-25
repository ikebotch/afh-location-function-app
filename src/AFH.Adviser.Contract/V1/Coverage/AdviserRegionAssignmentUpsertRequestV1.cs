namespace AFH.Adviser.Contract.V1.Coverage;

public sealed record AdviserRegionAssignmentUpsertRequestV1(
    string? AdviserId,
    string? AdviserName,
    string? Role,
    bool? IsLead,
    bool? IsActive);
