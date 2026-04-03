using AFH.BackendPlatform;
using AFH.Location.Function.Middleware;
using AFH.Location.Function.Security;
using System.Net;

namespace AFH.Location.Tests;

public class InternalApiAuthMiddlewareTests
{
    [Theory]
    [InlineData("LocationHealthV1", EndpointAccessPolicy.Public)]
    [InlineData("OpenApiV1", EndpointAccessPolicy.Public)]
    [InlineData("ScalarUi", EndpointAccessPolicy.Public)]
    [InlineData("LocationSearchV1", EndpointAccessPolicy.InternalOnly)]
    public void EndpointAccessPolicies_ClassifiesFunctions(string functionName, EndpointAccessPolicy expected)
    {
        Assert.Equal(expected, EndpointAccessPolicies.GetPolicy(functionName));
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
