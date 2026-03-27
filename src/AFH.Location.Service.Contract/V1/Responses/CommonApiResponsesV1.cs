namespace AFH.Location.Service.Contract.V1.Responses;

public sealed class ApiErrorResponseV1
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string>? Errors { get; init; }
}

public sealed class HealthStatusResponseV1
{
    public string Status { get; init; } = string.Empty;
}

public sealed class SyncAdviserCacheResponseV1
{
    public int Synced { get; init; }
}
