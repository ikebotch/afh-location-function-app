using AFH.Location.Application.Abstractions.Search;

using AFH.Location.Application.Models.V1.Results;
using System.Buffers;
using System.Text;
using System.Text.Json;

namespace AFH.Location.Application.Services.V1;

public sealed class LocationSearchAuditEntryFactory
{
    internal SearchAuditEntry Create(LocationSearchContext ctx)
    {
        var selected = ctx.Response.Candidates
            .OrderBy(x => x.Rank)
            .FirstOrDefault();

        return new SearchAuditEntry
        {
            RequestId = string.IsNullOrWhiteSpace(ctx.Request.RequestId) ? Guid.NewGuid().ToString("N") : ctx.Request.RequestId,
            CreatedUtc = DateTime.UtcNow,
            RequestedStartUtc = ctx.Request.Meeting.RequestedStartUtc,
            DurationMinutes = ctx.Request.Meeting.DurationMinutes,
            SearchHorizonMinutes = ctx.Request.Meeting.SearchHorizonMinutes,
            DestinationPostcode = ctx.Request.Destination.Address?.Postcode,
            RegionsCsv = ctx.Request.Filters?.Regions is { Length: > 0 } regions ? string.Join(",", regions) : null,
            CandidatesReturned = ctx.Response.Candidates.Count,
            SelectedAdviserId = selected?.AdviserId,
            SelectedAdviserRating = selected?.AdviserRating,
            SelectedAdviserGoldStar = selected?.GoldStar,
            SelectedTravelMinutes = selected?.TravelToClient.EtaMinutes,
            SelectedMaxTravelTimeMinutes = selected?.Buffers.MaxTravelTimeMinutes,
            SelectedCompanyBufferMinutes = selected?.Buffers.CompanyBufferMinutes,
            SelectedTravelBufferMinutes = selected?.Buffers.TravelBufferMinutes,
            SelectedOriginSource = GetOriginSource(selected),
            PayloadJson = BuildPayloadJson(ctx.Response.Candidates)
        };
    }

    private static string? GetOriginSource(LocationSearchCandidate? candidate)
        => candidate?.Reasons.FirstOrDefault(r =>
            r.StartsWith("ORIGIN_SOURCE_", StringComparison.OrdinalIgnoreCase));

    private static string BuildPayloadJson(IReadOnlyList<LocationSearchCandidate> candidates)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);

        writer.WriteStartArray();

        foreach (var candidate in candidates)
        {
            writer.WriteStartObject();
            writer.WriteString(nameof(candidate.AdviserId), candidate.AdviserId);
            writer.WriteNumber(nameof(candidate.Rank), candidate.Rank);

            if (candidate.Score.HasValue)
                writer.WriteNumber(nameof(candidate.Score), candidate.Score.Value);
            else
                writer.WriteNull(nameof(candidate.Score));

            writer.WriteNumber(nameof(candidate.AdviserRating), candidate.AdviserRating);
            writer.WriteBoolean(nameof(candidate.GoldStar), candidate.GoldStar);
            writer.WriteString(nameof(candidate.Availability), candidate.Availability);
            writer.WriteBoolean(nameof(candidate.Coverage.WithinCoverage), candidate.Coverage.WithinCoverage);
            if (candidate.TravelToClient.EtaMinutes.HasValue)
                writer.WriteNumber("TravelEtaMinutes", candidate.TravelToClient.EtaMinutes.Value);
            else
                writer.WriteNull("TravelEtaMinutes");
            writer.WriteNumber(nameof(candidate.Buffers.MaxTravelTimeMinutes), candidate.Buffers.MaxTravelTimeMinutes);
            writer.WriteNumber(nameof(candidate.Buffers.CompanyBufferMinutes), candidate.Buffers.CompanyBufferMinutes);
            writer.WriteNumber(nameof(candidate.Buffers.TravelBufferMinutes), candidate.Buffers.TravelBufferMinutes);

            var originSource = GetOriginSource(candidate);
            if (originSource is not null)
                writer.WriteString("OriginSource", originSource);
            else
                writer.WriteNull("OriginSource");

            writer.WritePropertyName("FailureReasons");
            writer.WriteStartArray();
            foreach (var reason in candidate.Reasons)
            {
                if (reason.Contains("EXCEEDED", StringComparison.OrdinalIgnoreCase) ||
                    reason.Contains("FAIL", StringComparison.OrdinalIgnoreCase) ||
                    reason.Contains("OUT_OF_COVERAGE", StringComparison.OrdinalIgnoreCase))
                {
                    writer.WriteStringValue(reason);
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.Flush();

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}