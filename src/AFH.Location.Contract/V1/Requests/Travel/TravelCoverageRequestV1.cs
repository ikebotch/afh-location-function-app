using System.Text.Json.Serialization;
using System.Text.Json;

using AFH.Location.Contract.V1.Docs;

namespace AFH.Location.Contract.V1.Requests.Travel;

[OpenApiExample("""
{
  "sourcePostcode": "B1 1AA",
  "timeContext": {
    "travelEvaluationMode": "TimeDependent",
    "slotResponseMode": "Grouped",
    "startTime": "2026-05-22T09:00:00Z",
    "endTime": "2026-05-22T17:00:00Z",
    "searchIntervalMinutes": 30
  },
  "destinations": [
    {
      "correlationId": "client-123",
      "postcode": "CV1 1AA",
      "maxTravelTimeMinutes": 60,
      "maxDistanceMiles": 50
    }
  ],
  "requestContext": {
    "correlationId": "req-456"
  }
}
""")]
public sealed record TravelCoverageRequestV1
{
    public string SourcePostcode { get; init; } = string.Empty;
    public TravelCoverageTimeContextV1 TimeContext { get; init; } = new();
    public IReadOnlyList<TravelCoverageDestinationRequestV1> Destinations { get; init; } = [];
    public LocationRequestContextV1 RequestContext { get; init; } = new();
}

public sealed record TravelCoverageTimeContextV1
{
    public TravelEvaluationModeV1 TravelEvaluationMode { get; init; } = TravelEvaluationModeV1.TimeIndependent;
    public SlotResponseModeV1 SlotResponseMode { get; init; } = SlotResponseModeV1.Grouped;
    /// <summary>Window start for slot generation (inclusive).</summary>
    public DateTimeOffset? StartTime { get; init; }
    /// <summary>Window end for slot generation (exclusive).</summary>
    public DateTimeOffset? EndTime { get; init; }
    /// <summary>Duration of each slot in minutes.</summary>
    public int? SearchIntervalMinutes { get; init; }
}

[JsonConverter(typeof(CaseInsensitiveEnumConverter<TravelEvaluationModeV1>))]
public enum TravelEvaluationModeV1
{
    TimeIndependent = 0,
    TimeDependent = 1
}

[JsonConverter(typeof(CaseInsensitiveEnumConverter<SlotResponseModeV1>))]
public enum SlotResponseModeV1
{
    Grouped = 0,
    Expanded = 1,
    Summary = 2
}

public class CaseInsensitiveEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (Enum.TryParse<T>(str, ignoreCase: true, out var result))
            {
                return result;
            }
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            var val = reader.GetInt32();
            if (Enum.IsDefined(typeof(T), val))
            {
                return (T)(object)val;
            }
        }
        
        throw new JsonException($"Unable to parse enum value to {typeof(T).Name}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public sealed record TravelCoverageDestinationRequestV1
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Postcode { get; init; } = string.Empty;
    public int? MaxTravelTimeMinutes { get; init; }
    public double? MaxDistanceMiles { get; init; }
}

public sealed record LocationRequestContextV1
{
    public string? CorrelationId { get; init; }
}
