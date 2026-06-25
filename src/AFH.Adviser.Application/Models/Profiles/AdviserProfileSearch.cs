namespace AFH.Adviser.Application.Models.Profiles;

public sealed record AdviserProfileSearch(
    string? Search,
    string? Region,
    bool IncludeInactive);
