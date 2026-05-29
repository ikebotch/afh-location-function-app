using AFH.Location.Function.Middleware;
using AFH.Location.Function.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Reflection;
using AFH.Location.Domain;
using AFH.Location.Function.Functions.V1.Location;

namespace AFH.Location.Tests;

public class InternalApiAuthMiddlewareTests
{
    [Theory]
    [InlineData("LocationHealthV1", EndpointAccessPolicy.Public)]
    [InlineData("OpenApiV1", EndpointAccessPolicy.Public)]
    [InlineData("ScalarUi", EndpointAccessPolicy.Public)]
    [InlineData("LicenseListV1", EndpointAccessPolicy.InternalOnly)]
    [InlineData("RouteTimeV1", EndpointAccessPolicy.InternalOnly)]
    [InlineData("SyncAdviserCacheV1", EndpointAccessPolicy.InternalOnly)]
    [InlineData("TravelCoverageV1", EndpointAccessPolicy.InternalOnly)]
    [InlineData("OrganisationAssignmentsListV1", EndpointAccessPolicy.UserAuthenticated)]
    [InlineData("OrganisationAssignmentsCreateV1", EndpointAccessPolicy.UserAuthenticated)]
    [InlineData("OrganisationAssignmentsUpdateV1", EndpointAccessPolicy.UserAuthenticated)]
    [InlineData("OrganisationAssignmentsDisableV1", EndpointAccessPolicy.UserAuthenticated)]
    [InlineData("OrganisationAssignmentsDeleteV1", EndpointAccessPolicy.UserAuthenticated)]
    public void EndpointAccessPolicies_ClassifiesFunctions(string functionName, EndpointAccessPolicy expected)
    {
        Assert.Equal(expected, EndpointAccessPolicies.GetPolicy(functionName));
    }

    [Fact]
    public void EndpointAccessPolicies_CoversEveryHttpTriggeredFunctionExplicitly()
    {
        var httpFunctionNames = typeof(TravelCoverageFunctionV1).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .Where(method => method.GetCustomAttribute<FunctionAttribute>() is not null)
            .Where(method => method.GetParameters().Any(parameter => parameter.GetCustomAttributes<HttpTriggerAttribute>(inherit: false).Any()))
            .Select(method => method.GetCustomAttribute<FunctionAttribute>()!.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var configuredNames = EndpointAccessPolicies.KnownHttpFunctions
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(configuredNames, httpFunctionNames);
    }

    [Fact]
    public void EndpointAccessPolicies_ThrowsForUnknownFunction()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => EndpointAccessPolicies.GetPolicy("UnmappedHttpFunction"));

        Assert.Contains("No endpoint access policy is configured", exception.Message);
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
