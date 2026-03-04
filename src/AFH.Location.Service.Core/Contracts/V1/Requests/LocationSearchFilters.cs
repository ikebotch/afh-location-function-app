namespace AFH.Location.Service.Core.Contracts.V1.Requests;


public sealed class LocationSearchFilters
{
    public string[]? Regions { get; init; }
    public string[]? AdviserIds { get; init; }
    public string[]? RequiredSkills { get; init; }
    public string[]? PreferredAdviserIds { get; init; }
    public string[]? ExcludeAdviserIds { get; init; }
    public int? MaxCandidates { get; init; }
    public int? BufferMinutes { get; init; }
    public double? MinAdviserRating { get; init; }
    public double? MaxRankingScore { get; init; }

}
