using AFH.Location.Contract.V1.Docs;

namespace AFH.Location.Contract.V1.Requests;

[OpenApiExample("""
{
  "requests": [
    {
      "requestId": "req-1",
      "meeting": { "requestedStartUtc": "2026-05-25T14:00:00Z", "durationMinutes": 60, "searchHorizonMinutes": 10080 },
      "destination": { "coordinates": { "lat": 52.4862, "lng": -1.8904 } },
      "filters": { "regions": ["Midlands"] }
    }
  ]
}
""")]
public sealed record LocationSearchBatchRequestV1
{
    public List<LocationSearchRequestV1> Requests { get; set; } = new();
}
