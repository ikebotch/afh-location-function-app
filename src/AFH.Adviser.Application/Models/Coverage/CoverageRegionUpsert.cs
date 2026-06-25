namespace AFH.Adviser.Application.Models.Coverage;

public sealed record CoverageRegionUpsert(
    string Code,
    string Name,
    string? LeadAdviserId,
    string? LeadAdviserName,
    IReadOnlyList<string> Postcodes,
    IReadOnlyList<string> Skills,
    double CoverageRadiusMiles,
    int MaxTravelTimeMinutes,
    bool IsActive);
