using AFH.Adviser.Application.Abstractions.Profiles;
using AFH.Adviser.Application.Abstractions.Repositories;
using AFH.Adviser.Application.Models.Profiles;
using AdviserEntity = AFH.Adviser.Domain.Entities.Adviser;

namespace AFH.Adviser.Application.Services.Profiles;

public sealed class AdviserProfileAdminService : IAdviserProfileAdminService
{
    private readonly IAdviserReferenceCacheRepository _cache;

    public AdviserProfileAdminService(IAdviserReferenceCacheRepository cache)
    {
        _cache = cache;
    }

    public async Task<IReadOnlyList<AdviserEntity>> SearchAsync(AdviserProfileSearch search, CancellationToken ct)
    {
        var advisers = await _cache.GetAllAsync(null, ct);
        var query = advisers.AsEnumerable();

        if (!search.IncludeInactive)
            query = query.Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(search.Region))
            query = query.Where(x => string.Equals(x.Region, search.Region.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            query = query.Where(x =>
                Contains(x.AdviserId, term) ||
                Contains(x.DisplayName, term) ||
                Contains(x.MailboxUserId, term) ||
                Contains(x.Region, term) ||
                x.Skills.Any(skill => Contains(skill, term)));
        }

        return query.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<AdviserEntity?> GetAsync(string adviserId, CancellationToken ct)
        => (await _cache.GetAllAsync([adviserId], ct)).FirstOrDefault();

    public async Task<AdviserEntity> UpsertAsync(AdviserProfileUpsert upsert, CancellationToken ct)
    {
        var adviser = new AdviserEntity
        {
            AdviserId = upsert.AdviserId.Trim(),
            DisplayName = upsert.DisplayName.Trim(),
            MailboxUserId = upsert.MailboxUserId?.Trim() ?? string.Empty,
            HomePostcode = upsert.HomePostcode?.Trim() ?? string.Empty,
            Region = upsert.Region?.Trim() ?? string.Empty,
            BaseOfficeId = upsert.BaseOfficeId?.Trim(),
            TeamName = upsert.TeamName?.Trim(),
            ManagerId = upsert.ManagerId?.Trim(),
            Skills = upsert.Skills
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Rating = upsert.Rating,
            IsActive = upsert.IsActive,
            IsBookable = upsert.IsBookable,
            CoverageRadiusMiles = upsert.CoverageRadiusMiles,
            MaxTravelTimeMinutes = upsert.MaxTravelTimeMinutes,
            LastSyncedUtc = DateTime.UtcNow
        };

        await _cache.UpsertAsync([adviser], DateTime.UtcNow, ct);
        return adviser;
    }

    public Task<bool> DisableAsync(string adviserId, CancellationToken ct)
        => _cache.SetActiveAsync(adviserId, false, ct);

    public Task<bool> DeleteAsync(string adviserId, CancellationToken ct)
        => _cache.DeleteAsync(adviserId, ct);

    private static bool Contains(string? value, string term)
        => !string.IsNullOrWhiteSpace(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);
}
