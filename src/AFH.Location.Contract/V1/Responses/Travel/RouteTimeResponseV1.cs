using System.Text.Json.Serialization;

using AFH.Location.Contract.V1.Docs;

namespace AFH.Location.Contract.V1.Responses.Travel;

[OpenApiExample("""
{
  "correlationId": "rt-123",
  "travelTimeMinutes": 35,
  "travelDistanceMiles": 22.4,
  "status": "Succeeded",
  "warnings": []
}
""")]
public sealed record RouteTimeResponseV1
{
    public string? CorrelationId { get; init; }
    public int? TravelTimeMinutes { get; init; }
    public double? TravelDistanceMiles { get; init; }
    public RouteTimeStatusV1 Status { get; init; }
    public IReadOnlyList<ApiWarning> Warnings { get; init; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RouteTimeStatusV1
{
    Succeeded = 0,
    RouteUnavailable = 1,
    Failed = 2
}
