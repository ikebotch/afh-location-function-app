using AFH.Adviser.Application.Abstractions.OrganisationAssignments;
using AFH.Adviser.Application.Models.OrganisationAssignments;

namespace AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;

public sealed class InMemoryOrganisationAssignmentDirectory : IOrganisationAssignmentDirectory
{
    private readonly List<OrganisationAssignment> _assignments = [];

    public Task<IReadOnlyList<OrganisationAssignment>> SearchAsync(OrganisationAssignmentSearch search, CancellationToken ct)
    {
        var rows = _assignments.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search.Context))
            rows = rows.Where(x => string.Equals(x.Context, search.Context.Trim(), StringComparison.OrdinalIgnoreCase));

        if (search.AssignmentTypes.Count > 0)
        {
            var types = search.AssignmentTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            rows = rows.Where(x => types.Contains(x.AssignmentType));
        }

        if (!search.IncludeDisabled)
            rows = rows.Where(x => x.IsEnabled);

        return Task.FromResult<IReadOnlyList<OrganisationAssignment>>(rows.OrderBy(x => x.Priority).ToArray());
    }

    public Task<OrganisationAssignment?> GetAsync(Guid id, CancellationToken ct)
        => Task.FromResult(_assignments.FirstOrDefault(x => x.Id == id));

    public Task<OrganisationAssignment> CreateAsync(OrganisationAssignmentUpsert request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var assignment = new OrganisationAssignment(
            Guid.NewGuid(),
            request.Context.Trim(),
            request.AssignmentType.Trim(),
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
        _assignments.Add(assignment);
        return Task.FromResult(assignment);
    }

    public Task<OrganisationAssignment?> UpdateAsync(Guid id, OrganisationAssignmentUpsert request, CancellationToken ct)
    {
        var index = _assignments.FindIndex(x => x.Id == id);
        if (index < 0)
            return Task.FromResult<OrganisationAssignment?>(null);

        var existing = _assignments[index];
        var updated = existing with
        {
            Context = request.Context.Trim(),
            AssignmentType = request.AssignmentType.Trim(),
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
        _assignments[index] = updated;
        return Task.FromResult<OrganisationAssignment?>(updated);
    }

    public Task<bool> DisableAsync(Guid id, CancellationToken ct)
    {
        var index = _assignments.FindIndex(x => x.Id == id);
        if (index < 0)
            return Task.FromResult(false);

        _assignments[index] = _assignments[index] with { IsEnabled = false, UpdatedUtc = DateTime.UtcNow };
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var removed = _assignments.RemoveAll(x => x.Id == id) > 0;
        return Task.FromResult(removed);
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

