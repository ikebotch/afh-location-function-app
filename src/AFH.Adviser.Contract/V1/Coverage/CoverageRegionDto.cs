namespace AFH.Adviser.Contract.V1.Coverage;

public sealed record CoverageRegionDto(
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
    IReadOnlyList<AdviserRegionAssignmentDto> AdviserAssignments);
