namespace AFH.Location.Domain.Entities;

public sealed class AdviserCandidate
{
    public Adviser Adviser { get; init; } = default!;
    public bool IsPreferred { get; init; }
    public List<string> SourceReasons { get; init; } = new();
}