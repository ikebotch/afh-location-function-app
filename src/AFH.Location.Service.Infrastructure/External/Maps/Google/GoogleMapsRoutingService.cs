using AFH.Location.Service.Application.Abstractions;

namespace AFH.Location.Service.Infrastructure.External.Maps.Google;

public sealed class GoogleMapsRoutingService : IRoutingService
{
    public GoogleMapsRoutingService(IHttpClientFactory httpFactory, Microsoft.Extensions.Configuration.IConfiguration cfg) { }

    public async Task<RouteResult> GetRouteAsync(
        (double Lat, double Lng) origin,
        (double Lat, double Lng) destination,
        CancellationToken ct)
    {
        await Task.CompletedTask;
        throw new NotSupportedException("Google routing is disabled because the provider path is incomplete and must not return fake routing data.");
    }
}
