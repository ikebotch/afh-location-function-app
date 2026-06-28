namespace AFH.Adviser.Contract.V1.Responses;

public sealed class SyncAdviserCacheResponseV1
{
    public int Synced { get; init; }
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Unchanged { get; init; }
    public bool Changed => Created + Updated > 0;
}
