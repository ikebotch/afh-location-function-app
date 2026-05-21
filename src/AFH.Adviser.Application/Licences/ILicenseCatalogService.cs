namespace AFH.Adviser.Application.Licences;

public interface ILicenseCatalogService
{
    Task<LicenseCatalogResult> GetLicensesAsync(CancellationToken ct);
}
