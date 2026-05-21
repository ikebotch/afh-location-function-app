using AFH.Location.Application.Licences;

namespace AFH.Location.Application.Abstractions.Licences;

public interface ILicenseCatalogService
{
    Task<LicenseCatalogResult> GetLicensesAsync(CancellationToken ct);
}
