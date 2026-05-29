using AFH.Location.Application.Models.BusinessContacts;

namespace AFH.Location.Application.Abstractions.BusinessContacts;

public interface IBusinessContactDirectory
{
    Task<IReadOnlyList<BusinessContact>> SearchAsync(BusinessContactSearch search, CancellationToken ct);
    Task<BusinessContact?> GetAsync(Guid id, CancellationToken ct);
    Task<BusinessContact> CreateAsync(BusinessContactUpsert request, CancellationToken ct);
    Task<BusinessContact?> UpdateAsync(Guid id, BusinessContactUpsert request, CancellationToken ct);
    Task<bool> DisableAsync(Guid id, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}

