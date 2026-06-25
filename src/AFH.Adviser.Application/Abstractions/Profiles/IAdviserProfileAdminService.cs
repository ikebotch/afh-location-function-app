using AFH.Adviser.Application.Models.Profiles;
using AdviserEntity = AFH.Adviser.Domain.Entities.Adviser;

namespace AFH.Adviser.Application.Abstractions.Profiles;

public interface IAdviserProfileAdminService
{
    Task<IReadOnlyList<AdviserEntity>> SearchAsync(AdviserProfileSearch search, CancellationToken ct);
    Task<AdviserEntity?> GetAsync(string adviserId, CancellationToken ct);
    Task<AdviserEntity> UpsertAsync(AdviserProfileUpsert upsert, CancellationToken ct);
    Task<bool> DisableAsync(string adviserId, CancellationToken ct);
    Task<bool> DeleteAsync(string adviserId, CancellationToken ct);
}
