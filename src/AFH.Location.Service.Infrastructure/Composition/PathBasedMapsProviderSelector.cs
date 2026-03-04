using AFH.Location.Service.Core.Abstractions;

namespace AFH.Location.Service.Infrastructure.Composition;

public sealed class PathBasedMapsProviderSelector : IMapsProviderSelector
{
    private readonly IRequestContextAccessor _ctx;

    public PathBasedMapsProviderSelector(IRequestContextAccessor ctx) => _ctx = ctx;

    public bool IsV2Request()
        => (_ctx.Path ?? "").Contains("/api/v2/", StringComparison.OrdinalIgnoreCase);
}