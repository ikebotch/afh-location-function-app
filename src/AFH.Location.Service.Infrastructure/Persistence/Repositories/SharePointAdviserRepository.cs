using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Domain.Entities;
using AFH.Location.Service.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Serialization;

namespace AFH.Location.Service.Infrastructure.Persistence.Repositories;

public sealed class SharePointAdviserRepository : IAdviserRepository
{
    private readonly GraphServiceClient _graph;
    private readonly SharePointAdviserOptions _opts;
    private readonly ILogger<SharePointAdviserRepository> _logger;

    public SharePointAdviserRepository(
        GraphServiceClient graph,
        IOptions<SharePointAdviserOptions> opts,
        ILogger<SharePointAdviserRepository> logger)
    {
        _graph = graph;
        _opts = opts.Value;
        _logger = logger;
    }

    // ------------------------------------------------------------
    // Multi-choice parser (Kiota-safe using JSON conversion)
    // ------------------------------------------------------------



  



private static string[] GetMultiChoice(IDictionary<string, object> fields, string key)
{
    if (!fields.TryGetValue(key, out var raw) || raw is not UntypedArray ua)
        return Array.Empty<string>();

    var result = new List<string>();

    var items = ua.GetValue();
    if (items == null)
        return Array.Empty<string>();

    foreach (var node in items)
    {
        if (node is UntypedString s)
        {
            var value = s.GetValue() as string;

            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value);
        }
    }

    return result.ToArray();
}
// ------------------------------------------------------------
// Main load method
// ------------------------------------------------------------
public async Task<IReadOnlyList<Adviser>> GetAllAsync(
                    IReadOnlyCollection<string>? adviserIds,
                    CancellationToken ct)
    {



        if (string.IsNullOrWhiteSpace(_opts.SiteId) || string.IsNullOrWhiteSpace(_opts.ListId))
            throw new InvalidOperationException("SharePoint SiteId and AdvisersListId must be configured.");

        // Load SharePoint fields (debugging)
        var spFields = await GetFieldsAsync();
        foreach (var f in spFields)
        {
            _logger.LogInformation(
                "Field: DisplayName={DisplayName}, InternalName={Type},InternalName={Name}, Id={Id}",
                f.DisplayName, f.Type, f.Name, f.Id);
        }

        // Filter logic
        var ids = (adviserIds ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var listItems = new List<ListItem>();

        if (ids.Count == 0)
        {
            _logger.LogInformation("Loading ALL advisers from SharePoint siteId={SiteId} listId={ListId}",
                _opts.SiteId, _opts.ListId);

            var page = await _graph
                .Sites[_opts.SiteId]
                .Lists[_opts.ListId]
                .Items
                .GetAsync(cfg =>
                {
                    cfg.QueryParameters.Expand = new[] { "fields" };
                    cfg.QueryParameters.Top = 999;
                }, ct);

            if (page?.Value is { Count: > 0 })
                listItems.AddRange(page.Value);
        }
        else
        {
            _logger.LogInformation("Loading {Count} advisers by AdviserId from SharePoint siteId={SiteId} listId={ListId}",
                ids.Count, _opts.SiteId, _opts.ListId);

            foreach (var filter in BuildOrFilters(ids, _opts.AdviserIdField, chunkSize: 15))
            {
                var page = await _graph
                    .Sites[_opts.SiteId]
                    .Lists[_opts.ListId]
                    .Items
                    .GetAsync(cfg =>
                    {
                        cfg.QueryParameters.Expand = new[] { "fields" };
                        cfg.QueryParameters.Filter = filter;
                        cfg.QueryParameters.Top = 999;
                    }, ct);

                if (page?.Value is { Count: > 0 })
                    listItems.AddRange(page.Value);
            }
        }

        // Map to domain
        var advisers = new List<Adviser>(listItems.Count);

        foreach (var li in listItems)
        {
            var fields = li.Fields?.AdditionalData;
            if (fields is null) continue;

            var adviserId = TryGet(fields, _opts.AdviserIdField);
            if (string.IsNullOrWhiteSpace(adviserId))
                continue;

            var name = TryGet(fields, _opts.NameField) ?? TryGet(fields, "Title") ?? adviserId;
            var postcode = TryGet(fields, _opts.PostcodeField);
            var region = TryGet(fields, _opts.RegionField);
            var rating = TryGetDouble(fields, _opts.RatingField) ?? _opts.DefaultRating;
            var coverageRadiusMiles = TryGetDouble(fields, _opts.CoverageRadiusMilesField);
            var maxTravelTimeMinutes = TryGetInt(fields, _opts.MaxTravelTimeMinutesField);

            // Multi-choice skills
            var skills = GetMultiChoice(fields, _opts.SkillsField);





            advisers.Add(new Adviser
            {
                AdviserId = adviserId,
                DisplayName = name,
                HomePostcode = postcode ?? "",
                Region = region ?? "",
                Skills = skills,
                Rating = rating,
                IsActive = true,
                CoverageRadiusMiles = coverageRadiusMiles is > 0 ? coverageRadiusMiles : null,
                MaxTravelTimeMinutes = maxTravelTimeMinutes is > 0 ? maxTravelTimeMinutes : null
            });
        }

        return advisers
            .GroupBy(a => a.AdviserId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------
    private static IEnumerable<string> BuildOrFilters(
        IReadOnlyList<string> adviserIds,
        string adviserIdInternalFieldName,
        int chunkSize)
    {
        for (var i = 0; i < adviserIds.Count; i += chunkSize)
        {
            var chunk = adviserIds.Skip(i).Take(chunkSize);
            var parts = chunk.Select(id =>
            {
                var safe = id.Replace("'", "''");
                return $"fields/{adviserIdInternalFieldName} eq '{safe}'";
            });

            yield return string.Join(" or ", parts);
        }
    }

    public async Task<IReadOnlyList<ColumnDefinition>> GetFieldsAsync()
    {
        var columns = await _graph
            .Sites[_opts.SiteId]
            .Lists[_opts.ListId]
            .Columns
            .GetAsync();

        return columns?.Value?.ToList() ?? new List<ColumnDefinition>();
    }

    private static string? TryGet(IDictionary<string, object> dict, string key)
        => dict.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static double? TryGetDouble(IDictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v is null) return null;
        if (double.TryParse(v.ToString(), out var parsed)) return parsed;
        return null;
    }

    private static int? TryGetInt(IDictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v is null) return null;
        if (int.TryParse(v.ToString(), out var parsed)) return parsed;
        return null;
    }
}
