namespace AFH.Adviser.Application.Models.Coverage;

public sealed record CoverageRegion(
    Guid Id,
    string Code,
    string Name,
    string? LeadAdviserId,
    string? LeadAdviserName,
    IReadOnlyList<string> Postcodes,
    IReadOnlyList<string> Skills,
    double CoverageRadiusMiles,
    int MaxTravelTimeMinutes,
    bool IsActive,
    DateTime CreatedUtc,
    DateTime? UpdatedUtc,
    IReadOnlyList<AdviserRegionAssignment> AdviserAssignments);
