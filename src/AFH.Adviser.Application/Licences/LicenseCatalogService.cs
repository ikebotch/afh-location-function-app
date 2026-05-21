using AFH.Adviser.Application.Abstractions;
namespace AFH.Adviser.Application.Licences;

public sealed class LicenseCatalogService : ILicenseCatalogService
{
    private readonly IAdviserRepository _adviserRepository;

    public LicenseCatalogService(IAdviserRepository adviserRepository)
    {
        _adviserRepository = adviserRepository;
    }

    public async Task<LicenseCatalogResult> GetLicensesAsync(CancellationToken ct)
    {
        var advisers = await _adviserRepository.GetAllAsync(null, ct);

        return new LicenseCatalogResult
        {
            Licenses = advisers
                .Where(a => a.IsActive)
                .SelectMany(a => a.Skills)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }
}
