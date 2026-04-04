namespace AFH.Location.Contract.V1.Requests;

public sealed class Filters
{
    public List<string>? Regions { get; set; }
    public List<string>? RequiredSkills { get; set; }
    public List<string>? PreferredAdviserIds { get; set; }
    public List<string>? ExcludeAdviserIds { get; set; }
}
