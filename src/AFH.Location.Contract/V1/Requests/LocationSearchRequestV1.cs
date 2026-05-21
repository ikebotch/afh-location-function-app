using AFH.Location.Contract.V1.Docs;

namespace AFH.Location.Contract.V1.Requests;

[OpenApiExample("""
{
  "requestId": "req-123",
  "meeting": {
    "requestedStartUtc": "2026-05-25T14:00:00Z",
    "durationMinutes": 60,
    "searchHorizonMinutes": 10080
  },
  "destination": {
    "coordinates": {
      "lat": 52.4862,
      "lng": -1.8904
    },
    "address": {
      "postcode": "B1 1AA"
    }
  },
  "filters": {
    "regions": ["Midlands"],
    "maxCandidates": 10,
    "bufferMinutes": 30
  }
}
""")]
public sealed record LocationSearchRequestV1
{
    public string RequestId { get; set; } = default!;
    public MeetingWindow Meeting { get; set; } = new();
    public Destination Destination { get; set; } = new();


    public LocationSearchFilters? Filters { get; init; }
}
