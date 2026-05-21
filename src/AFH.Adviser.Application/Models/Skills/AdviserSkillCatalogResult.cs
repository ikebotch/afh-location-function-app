namespace AFH.Adviser.Application.Models.Skills;

public sealed class AdviserSkillCatalogResult
{
    public IReadOnlyList<string> Skills { get; init; } = Array.Empty<string>();
}
