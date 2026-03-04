

namespace AFH.Location.Service.Core.Abstractions;

public interface IRequestContextAccessor
{
  string? Path { get; }
}
