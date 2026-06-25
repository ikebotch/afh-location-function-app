namespace AFH.Adviser.Application.Models.Coverage;

public sealed record CoverageRegionSearch(
    string? Search,
    string? RegionCode,
    string? AdviserId,
    bool IncludeInactive);
