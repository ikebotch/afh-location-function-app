using AFH.Location.Contract.V1.Docs;

namespace AFH.Location.Contract.V1.Responses;

[OpenApiExample("""
{
  "requestId": "req-123",
  "generatedAtUtc": "2026-05-21T12:00:00Z",
  "candidates": [
    {
      "adviserId": "adv-1",
      "mailboxUserId": "adv-1@example.com",
      "adviserRating": 5,
      "goldStar": true,
      "preferred": false,
      "availability": "Available",
      "proposedSlotUtc": {
        "start": "2026-05-25T14:00:00Z",
        "end": "2026-05-25T15:00:00Z"
      },
      "coverage": {
        "withinCoverage": true,
        "anchorPostcode": "B1 1AA",
        "distanceMiles": 5.2
      },
      "travelToClient": {
        "etaMinutes": 15,
        "distanceMiles": 5.2,
        "confidence": "High"
      },
      "travelToBase": {
        "homeMinutes": 30,
        "officeMinutes": 45
      },
      "travelToNearestOffice": {
        "officeId": "off-1",
        "etaMinutes": 20,
        "distanceMiles": 8.0,
        "confidence": "High"
      },
      "buffers": {
        "travelBufferMinutes": 15,
        "companyBufferMinutes": 0,
        "preMeetingBufferMinutes": 15,
        "postMeetingBufferMinutes": 15,
        "maxTravelTimeMinutes": 60
      },
      "reasons": ["High rating", "Within coverage", "Available slot"],
      "rank": 1,
      "score": 95.5
    }
  ],
  "warnings": []
}
""")]
public sealed class LocationSearchResponseV1
{
    public string RequestId { get; set; } = default!;
    public DateTime GeneratedAtUtc { get; set; }
    public List<LocationCandidate> Candidates { get; set; } = new();
    public List<ApiWarning> Warnings { get; set; } = new();
}
