namespace AFH.Location.Application.Models.V1.Requests;

public sealed class LocationSearchFilters
{
    public string[] Regions { get; set; } = [];
    public string[] AdviserIds { get; set; } = [];
    public string[] RequiredSkills { get; set; } = [];
    public string[] PreferredAdviserIds { get; set; } = [];
    public string[] ExcludeAdviserIds { get; set; } = [];
    public int? MaxCandidates { get; set; }
    public int? BufferMinutes { get; set; }
    public int? CompanyBufferMinutes { get; set; }
    public double? MinAdviserRating { get; set; }
    public double? MaxRankingScore { get; set; }
}
