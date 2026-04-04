using AFH.Location.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace AFH.Location.Application.Services.V1;

public sealed class LocationSearchAuditWriter
{
    private readonly LocationSearchAuditEntryFactory _entryFactory;
    private readonly ISearchAuditRepository _searchAuditRepository;
    private readonly ILogger<LocationSearchAuditWriter> _logger;

    public LocationSearchAuditWriter(
        LocationSearchAuditEntryFactory entryFactory,
        ISearchAuditRepository searchAuditRepository,
        ILogger<LocationSearchAuditWriter> logger)
    {
        _entryFactory = entryFactory;
        _searchAuditRepository = searchAuditRepository;
        _logger = logger;
    }

    internal async Task WriteAsync(LocationSearchContext ctx, CancellationToken ct)
    {
        try
        {
            var entry = _entryFactory.Create(ctx);
            await _searchAuditRepository.SaveAsync(entry, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search audit persistence failed. RequestId={RequestId}", ctx.Request.RequestId);
        }
    }
}
