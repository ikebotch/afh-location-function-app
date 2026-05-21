using AFH.Location.Contract.V1.Docs;

namespace AFH.Location.Contract.V1.Requests.Travel;

[OpenApiExample("""
{
  "correlationId": "rt-123",
  "departAt": "2026-05-22T09:00:00Z",
  "source": {
    "latitude": 52.4862,
    "longitude": -1.8904
  },
  "destination": {
    "latitude": 52.4068,
    "longitude": -1.5197
  }
}
""")]
public sealed record RouteTimeRequestV1
{
    public string? CorrelationId { get; init; }
    public DateTimeOffset DepartAt { get; init; }
    public RouteTimeCoordinatesV1 Source { get; init; } = new();
    public RouteTimeCoordinatesV1 Destination { get; init; } = new();
}

public sealed record RouteTimeCoordinatesV1
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
