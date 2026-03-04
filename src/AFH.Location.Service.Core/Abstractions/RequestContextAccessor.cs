namespace AFH.Location.Service.Core.Abstractions;

public sealed class RequestContextAccessor : IRequestContextAccessor
{
    private static readonly AsyncLocal<string?> _path = new();

    public string? Path => _path.Value;
    public static void SetPath(string path) => _path.Value = path;
}