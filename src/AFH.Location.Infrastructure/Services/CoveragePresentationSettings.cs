using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace AFH.Location.Infrastructure.Services;

public sealed class CoveragePresentationSettings : ICoveragePresentationSettings
{
    private readonly LocationCoverageOptions _options;

    public CoveragePresentationSettings(IOptions<LocationCoverageOptions> options)
    {
        _options = options.Value;
    }

    public double AverageTravelSpeedMph => Math.Max(1d, _options.AverageTravelSpeedMph);
}
