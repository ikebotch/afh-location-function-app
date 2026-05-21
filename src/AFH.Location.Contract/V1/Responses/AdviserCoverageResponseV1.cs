using AFH.Location.Contract.V1.Docs;

namespace AFH.Location.Contract.V1.Responses;

[OpenApiExample("""
{
  "advisers": [
    {
      "id": "adv-1",
      "name": "Jane Smith",
      "mailboxUserId": "jane.smith@example.com",
      "region": "Midlands",
      "postcode": "B1 1AA",
      "isActive": true,
      "skills": ["Pension", "Investment"],
      "rating": 5,
      "latitude": 52.4862,
      "longitude": -1.8904,
      "maxTravelTimeMinutes": 60,
      "radiusMiles": 50.0,
      "radiusKm": 80.4,
      "radiusSource": "Calculated"
    }
  ],
  "regions": [
    {
      "id": "reg-1",
      "name": "Midlands",
      "latitude": 52.4862,
      "longitude": -1.8904
    }
  ]
}
""")]
public sealed class AdviserCoverageResponseV1
{
    public IReadOnlyList<AdviserCoveragePointV1> Advisers { get; init; } = [];
    public IReadOnlyList<RegionCoveragePointV1> Regions { get; init; } = [];
}
