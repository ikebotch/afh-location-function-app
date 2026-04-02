using System.Text.Json.Serialization;

namespace AFH.Location.Function.Common;

public sealed class ApiEnvelope<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiPaging? Paging { get; init; }
}

public sealed class ApiPaging
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
}