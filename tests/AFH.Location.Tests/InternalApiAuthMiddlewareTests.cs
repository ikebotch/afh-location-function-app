using AFH.Location.Function.Middleware;
using System.Net;

namespace AFH.Location.Tests;

public class InternalApiAuthMiddlewareTests
{
    [Theory]
    [InlineData("/api/v1/location/health", true)]
    [InlineData("/api/openapi/v1.json", true)]
    [InlineData("/api/scalar", true)]
    [InlineData("/api/v1/location/inperson/advisers/search", false)]
    public void IsPublic_ReturnsExpectedValue(string path, bool expected)
    {
        Assert.Equal(expected, InternalApiAuthMiddleware.IsPublic(path));
    }

    [Fact]
    public void ValidateAuthorization_RejectsMissingBearerForProtectedRoutes()
    {
        var failure = InternalApiAuthMiddleware.ValidateAuthorization("expected-token", null);

        Assert.NotNull(failure);
        Assert.Equal(HttpStatusCode.Unauthorized, failure?.StatusCode);
    }

    [Fact]
    public void ValidateAuthorization_RejectsInvalidBearerForProtectedRoutes()
    {
        var failure = InternalApiAuthMiddleware.ValidateAuthorization("expected-token", "Bearer wrong-token");

        Assert.NotNull(failure);
        Assert.Equal(HttpStatusCode.Forbidden, failure?.StatusCode);
    }
}
