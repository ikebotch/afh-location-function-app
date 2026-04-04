using AFH.Location.Application.Abstractions;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AFH.Location.Application.Services.V1;

public sealed class LocationSearchAuditWriter
{
    private readonly ISearchAuditRepository _searchAuditRepository;
    private readonly ILogger<LocationSearchAuditWriter> _logger;

    public LocationSearchAuditWriter(
        ISearchAuditRepository searchAuditRepository,
        ILogger<LocationSearchAuditWriter> logger)
    {
        _searchAuditRepository = searchAuditRepository;
        _logger = logger;
    }

    internal async Task WriteAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        try
        {
            var selected = ctx.Response.Candidates
                .OrderBy(x => x.Rank)
                .FirstOrDefault();

            var payload = ctx.Response.Candidates
                .Select(x => new
                {
                    x.AdviserId,
                    x.Rank,
                    x.Score,
                    x.AdviserRating,
                    x.GoldStar,
                    x.Availability,
                    x.Coverage.WithinCoverage,
                    TravelEtaMinutes = x.TravelToClient.EtaMinutes,
                    x.Buffers.MaxTravelTimeMinutes,
                    x.Buffers.CompanyBufferMinutes,
                    x.Buffers.TravelBufferMinutes,
                    OriginSource = x.Reasons.FirstOrDefault(r => r.StartsWith("ORIGIN_SOURCE_", StringComparison.OrdinalIgnoreCase)),
                    FailureReasons = x.Reasons.Where(r =>
                        r.Contains("EXCEEDED", StringComparison.OrdinalIgnoreCase) ||
                        r.Contains("FAIL", StringComparison.OrdinalIgnoreCase) ||
                        r.Contains("OUT_OF_COVERAGE", StringComparison.OrdinalIgnoreCase))
                })
                .ToList();

            var entry = new SearchAuditEntry
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
                SelectedOriginSource = selected?.Reasons.FirstOrDefault(r =>
                    r.StartsWith("ORIGIN_SOURCE_", StringComparison.OrdinalIgnoreCase)),
                PayloadJson = JsonSerializer.Serialize(payload)
            };

            await _searchAuditRepository.SaveAsync(entry, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search audit persistence failed. RequestId={RequestId}", ctx.Request.RequestId);
        }
    }
}
