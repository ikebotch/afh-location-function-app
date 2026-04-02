using AFH.Location.Application.Models.V1;

namespace AFH.Location.Application.Abstractions;

public interface ILicenseCatalogService
{
    Task<LicenseCatalogResult> GetLicensesAsync(CancellationToken ct);
}
