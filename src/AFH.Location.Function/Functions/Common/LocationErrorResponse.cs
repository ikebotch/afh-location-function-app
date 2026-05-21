using System.Collections.Generic;

namespace AFH.Location.Function.Functions.Common;

public sealed class LocationErrorResponse
{
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
    public IReadOnlyList<string>? Errors { get; init; }
}
