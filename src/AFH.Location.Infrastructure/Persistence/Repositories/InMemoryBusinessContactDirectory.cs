using AFH.Location.Application.Abstractions.BusinessContacts;
using AFH.Location.Application.Models.BusinessContacts;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

public sealed class InMemoryBusinessContactDirectory : IBusinessContactDirectory
{
    private readonly List<BusinessContact> _contacts = [];

    public Task<IReadOnlyList<BusinessContact>> SearchAsync(BusinessContactSearch search, CancellationToken ct)
    {
        var rows = _contacts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search.Context))
            rows = rows.Where(x => string.Equals(x.Context, search.Context.Trim(), StringComparison.OrdinalIgnoreCase));

        if (search.ContactTypes.Count > 0)
        {
            var types = search.ContactTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            rows = rows.Where(x => types.Contains(x.ContactType));
        }

        if (!search.IncludeDisabled)
            rows = rows.Where(x => x.IsEnabled);

        return Task.FromResult<IReadOnlyList<BusinessContact>>(rows.OrderBy(x => x.Priority).ToArray());
    }

    public Task<BusinessContact?> GetAsync(Guid id, CancellationToken ct)
        => Task.FromResult(_contacts.FirstOrDefault(x => x.Id == id));

    public Task<BusinessContact> CreateAsync(BusinessContactUpsert request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var contact = new BusinessContact(
            Guid.NewGuid(),
            request.Context.Trim(),
            request.ContactType.Trim(),
            TrimToNull(request.OrganisationId),
            TrimToNull(request.ClientId),
            TrimToNull(request.Region),
            TrimToNull(request.AdviserId),
            request.DisplayName.Trim(),
            TrimToNull(request.Email),
            TrimToNull(request.MobileNumber),
            request.Channels.Count == 0 ? ["Email"] : request.Channels,
            request.IsEnabled,
            request.Priority,
            now,
            now);
        _contacts.Add(contact);
        return Task.FromResult(contact);
    }

    public Task<BusinessContact?> UpdateAsync(Guid id, BusinessContactUpsert request, CancellationToken ct)
    {
        var index = _contacts.FindIndex(x => x.Id == id);
        if (index < 0)
            return Task.FromResult<BusinessContact?>(null);

        var existing = _contacts[index];
        var updated = existing with
        {
            Context = request.Context.Trim(),
            ContactType = request.ContactType.Trim(),
            OrganisationId = TrimToNull(request.OrganisationId),
            ClientId = TrimToNull(request.ClientId),
            Region = TrimToNull(request.Region),
            AdviserId = TrimToNull(request.AdviserId),
            DisplayName = request.DisplayName.Trim(),
            Email = TrimToNull(request.Email),
            MobileNumber = TrimToNull(request.MobileNumber),
            Channels = request.Channels.Count == 0 ? ["Email"] : request.Channels,
            IsEnabled = request.IsEnabled,
            Priority = request.Priority,
            UpdatedUtc = DateTime.UtcNow
        };
        _contacts[index] = updated;
        return Task.FromResult<BusinessContact?>(updated);
    }

    public Task<bool> DisableAsync(Guid id, CancellationToken ct)
    {
        var index = _contacts.FindIndex(x => x.Id == id);
        if (index < 0)
            return Task.FromResult(false);

        _contacts[index] = _contacts[index] with { IsEnabled = false, UpdatedUtc = DateTime.UtcNow };
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var removed = _contacts.RemoveAll(x => x.Id == id) > 0;
        return Task.FromResult(removed);
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

