using AFH.Location.Contract.V1.Docs;

namespace AFH.Location.Contract.V1.Responses;

[OpenApiExample("""
{
  "generatedAtUtc": "2026-05-21T12:00:00Z",
  "results": [
    {
      "requestId": "req-1",
      "success": true,
      "result": {
        "requestId": "req-1",
        "generatedAtUtc": "2026-05-21T12:00:00Z",
        "candidates": [],
        "warnings": []
      }
    }
  ]
}
""")]
public sealed record LocationSearchBatchResponseV1
{
    public DateTime GeneratedAtUtc { get; set; }
    public List<LocationSearchBatchItemResponseV1> Results { get; set; } = new();
}
