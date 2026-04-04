using AFH.Location.Function.Middleware;

namespace AFH.Location.Tests;

public sealed class ObservabilityConventionTests
{
    [Fact]
    public void CorrelationIdMiddleware_UsesCanonicalHeaderAndItemKey()
    {
        Assert.Equal("x-correlation-id", CorrelationIdMiddleware.HeaderName);
        Assert.Equal("correlation-id", CorrelationIdMiddleware.ItemKey);
    }
}
