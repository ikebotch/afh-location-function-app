using AFH.Location.Application.Abstractions.BusinessContacts;
using AFH.Location.Application.Models.BusinessContacts;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

public sealed class SqlBusinessContactDirectory : IBusinessContactDirectory
{
    private readonly LocationPolicyDbContext _db;

    public SqlBusinessContactDirectory(LocationPolicyDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<BusinessContact>> SearchAsync(BusinessContactSearch search, CancellationToken ct)
    {
        var query = _db.BusinessContacts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search.Context))
            query = query.Where(x => x.Context == search.Context.Trim());

        if (search.ContactTypes.Count > 0)
        {
            var types = search.ContactTypes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            query = query.Where(x => types.Contains(x.ContactType));
        }

        if (!string.IsNullOrWhiteSpace(search.OrganisationId))
            query = query.Where(x => x.OrganisationId == null || x.OrganisationId == search.OrganisationId.Trim());

        if (!string.IsNullOrWhiteSpace(search.ClientId))
            query = query.Where(x => x.ClientId == null || x.ClientId == search.ClientId.Trim());

        if (!string.IsNullOrWhiteSpace(search.Region))
            query = query.Where(x => x.Region == null || x.Region == search.Region.Trim());

        if (!string.IsNullOrWhiteSpace(search.AdviserId))
            query = query.Where(x => x.AdviserId == null || x.AdviserId == search.AdviserId.Trim());

        if (!search.IncludeDisabled)
            query = query.Where(x => x.IsEnabled);

        var rows = await query
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.ContactType)
            .ThenBy(x => x.DisplayName)
            .ToArrayAsync(ct);

        return rows.Select(ToModel).ToArray();
    }

    public async Task<BusinessContact?> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.BusinessContacts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : ToModel(row);
    }

    public async Task<BusinessContact> CreateAsync(BusinessContactUpsert request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var row = new BusinessContactEntity
        {
            Id = Guid.NewGuid(),
            CreatedUtc = now
        };
        Apply(row, request, now);
        _db.BusinessContacts.Add(row);
        await _db.SaveChangesAsync(ct);
        return ToModel(row);
    }

    public async Task<BusinessContact?> UpdateAsync(Guid id, BusinessContactUpsert request, CancellationToken ct)
    {
        var row = await _db.BusinessContacts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
            return null;

        Apply(row, request, DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return ToModel(row);
    }

    public async Task<bool> DisableAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.BusinessContacts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
            return false;

        row.IsEnabled = false;
        row.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.BusinessContacts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
            return false;

        _db.BusinessContacts.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void Apply(BusinessContactEntity row, BusinessContactUpsert request, DateTime now)
    {
        row.Context = Require(request.Context, "context");
        row.ContactType = Require(request.ContactType, "contactType");
        row.OrganisationId = TrimToNull(request.OrganisationId);
        row.ClientId = TrimToNull(request.ClientId);
        row.Region = TrimToNull(request.Region);
        row.AdviserId = TrimToNull(request.AdviserId);
        row.DisplayName = Require(request.DisplayName, "displayName");
        row.Email = TrimToNull(request.Email);
        row.MobileNumber = TrimToNull(request.MobileNumber);
        row.Channels = string.Join(',', NormaliseChannels(request.Channels));
        row.IsEnabled = request.IsEnabled;
        row.Priority = request.Priority;
        row.UpdatedUtc = now;
    }

    private static BusinessContact ToModel(BusinessContactEntity row)
        => new(
            row.Id,
            row.Context,
            row.ContactType,
            row.OrganisationId,
            row.ClientId,
            row.Region,
            row.AdviserId,
            row.DisplayName,
            row.Email,
            row.MobileNumber,
            SplitChannels(row.Channels),
            row.IsEnabled,
            row.Priority,
            row.CreatedUtc,
            row.UpdatedUtc);

    private static string Require(string? value, string field)
        => !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"{field} is required.");

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> SplitChannels(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static IReadOnlyList<string> NormaliseChannels(IReadOnlyList<string> channels)
    {
        var values = channels
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? ["Email"] : values;
    }
}

