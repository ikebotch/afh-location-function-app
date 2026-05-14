using AFH.Location.Application.Models.V1;

namespace AFH.Location.Application.Abstractions.Search;

public interface ILicenseCatalogService
{
    Task<LicenseCatalogResult> GetLicensesAsync(CancellationToken ct);
}
