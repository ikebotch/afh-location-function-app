using AFH.Location.Service.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using System.Text.Encodings.Web;

namespace AFH.Location.Service.Infrastructure.External.Maps.Google;

public sealed class GoogleMapsRoutingService : IRoutingService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public GoogleMapsRoutingService(IHttpClientFactory httpFactory, IConfiguration cfg)
    {
        _http = httpFactory.CreateClient(nameof(GoogleMapsRoutingService));
        _cfg = cfg;
    }

    public async Task<RouteResult> GetRouteAsync(
        (double Lat, double Lng) origin,
        (double Lat, double Lng) destination,
        CancellationToken ct)
    {
        // TODO (v2): implement Google Directions API call.
        // For now return "Low" confidence to prove wiring works.
        await Task.CompletedTask;
        return new RouteResult(EtaMinutes: 0, DistanceMiles: 0, Confidence: "Low");
    }
}