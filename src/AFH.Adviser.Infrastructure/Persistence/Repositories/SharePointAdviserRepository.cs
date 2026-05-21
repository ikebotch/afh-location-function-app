using AFH.Adviser.Application.Services.Skills;
using AFH.Common.SharePointUtils.Abstractions;
using AFH.Common.SharePointUtils.Extensions;
using AFH.Adviser.Application.Abstractions.Repositories;
using Entities = AFH.Adviser.Domain.Entities;

using AFH.Adviser.Domain.Entities;
using AFH.Adviser.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;

namespace AFH.Adviser.Infrastructure.Persistence.Repositories;

public sealed class SharePointAdviserRepository : IAdviserSourceRepository
{
    private readonly ISharePointListService _sharePointListService;
    private readonly ISharePointFieldResolver _fieldResolver;
    private readonly SharePointAdviserOptions _opts;
    private readonly ILogger<SharePointAdviserRepository> _logger;

    public SharePointAdviserRepository(
        ISharePointListService sharePointListService,
        ISharePointFieldResolver fieldResolver,
        IOptions<SharePointAdviserOptions> opts,
        ILogger<SharePointAdviserRepository> logger)
    {
        _sharePointListService = sharePointListService;
        _fieldResolver = fieldResolver;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Entities.Adviser>> GetAllAsync(
        IReadOnlyCollection<string>? adviserIds,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opts.SiteId) || string.IsNullOrWhiteSpace(_opts.ListId))
            throw new InvalidOperationException("SharePoint SiteId and AdvisersListId must be configured.");

        // Filter logic
        var ids = (adviserIds ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var fieldProfile = await _fieldResolver.ResolveProfileAsync(
            _opts.SiteId,
            _opts.ListId,
            new SharePointAdviserFieldProfile(_opts),
            ct);

        var listItems = new List<ListItem>();

        if (ids.Count == 0)
        {
            _logger.LogInformation("Loading ALL advisers from SharePoint siteId={SiteId} listId={ListId}",
                _opts.SiteId, _opts.ListId);

            var items = await _sharePointListService.GetListItems(
                _opts.SiteId,
                _opts.ListId,
                expandFields: ["fields"],
                cancellationToken: ct);

            listItems.AddRange(items);
        }
        else
        {
            _logger.LogInformation("Loading {Count} advisers by AdviserId from SharePoint siteId={SiteId} listId={ListId}",
                ids.Count, _opts.SiteId, _opts.ListId);

            foreach (var currentFilter in BuildOrFilters(
                ids,
                fieldProfile.GetRequiredInternalName(SharePointAdviserFieldNames.AdviserId),
                chunkSize: 15))
            {
                var items = await _sharePointListService.GetListItems(
                    _opts.SiteId,
                    _opts.ListId,
                    currentFilter,
                    expandFields: ["fields"],
                    cancellationToken: ct);

                listItems.AddRange(items);
            }
        }

        // Map to domain
        var advisers = new List<Entities.Adviser>(listItems.Count);

        foreach (var li in listItems)
        {
            var fields = li.GetFieldValues();
            if (fields.Count == 0) continue;

            var adviserId = fields.GetString(fieldProfile, SharePointAdviserFieldNames.AdviserId);
            if (string.IsNullOrWhiteSpace(adviserId))
                continue;

            var displayName = fields.GetString(fieldProfile, SharePointAdviserFieldNames.DisplayName);
            var name = fields.GetString(fieldProfile, SharePointAdviserFieldNames.Name)
                ?? displayName
                ?? adviserId;
            var postcode = fields.GetString(fieldProfile, SharePointAdviserFieldNames.Postcode);
            var region = fields.GetString(fieldProfile, SharePointAdviserFieldNames.Region);
            var rating = fields.GetDouble(fieldProfile.GetRequiredInternalName(SharePointAdviserFieldNames.Rating))
                ?? _opts.DefaultRating;
            var coverageRadiusMiles = fields.GetDouble(
                fieldProfile.GetRequiredInternalName(SharePointAdviserFieldNames.CoverageRadiusMiles));
            var maxTravelTimeMinutes = fields.GetInt32(
                fieldProfile.GetRequiredInternalName(SharePointAdviserFieldNames.MaxTravelTimeMinutes));
            var skills = fields.GetChoices(fieldProfile.GetRequiredInternalName(SharePointAdviserFieldNames.Skills));

            advisers.Add(new Entities.Adviser
            {
                AdviserId = adviserId,
                DisplayName = name,
                MailboxUserId = fields.GetString(fieldProfile, SharePointAdviserFieldNames.Email) ?? adviserId,
                HomePostcode = postcode ?? "",
                Region = region ?? "",
                Skills = SkillNormaliser.NormaliseSkills(skills),
                Rating = rating,
                IsActive = true,
                IsBookable = true,
                CoverageRadiusMiles = coverageRadiusMiles is > 0 ? coverageRadiusMiles : null,
                MaxTravelTimeMinutes = maxTravelTimeMinutes is > 0 ? maxTravelTimeMinutes : null,
                LastSyncedUtc = DateTime.UtcNow
            });
        }

        return advisers
            .GroupBy(a => a.AdviserId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

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
}
