using System.Text.Json.Serialization;

namespace AFH.Location.Function.Common;

public sealed class ApiEnvelope<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiPaging? Paging { get; init; }
}
