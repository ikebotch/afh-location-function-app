using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Application.Services.V1;
using AFH.Location.Service.Domain;
using AFH.Location.Service.Domain.Entities;

namespace AFH.Location.Service.UnitTests;

public class AdviserCoverageServiceV1Tests
{
    [Fact]
    public async Task GetCoverageAsync_RejectsInvalidZeroCoordinates()
    {
        var sut = new AdviserCoverageServiceV1(
            new StubAdviserRepository(),
            new StubOfficeRepository(),
            new StubGeocodingService(),
            new StubCoveragePolicyProvider(),
            new StubCoverageSettings());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetCoverageAsync(CancellationToken.None));

        Assert.Contains("invalid coordinates", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubAdviserRepository : IAdviserRepository
    {
        public Task<IReadOnlyList<Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<Adviser>>([
                new Adviser
                {
                    AdviserId = "adv-1",
                    DisplayName = "Adviser One",
                    HomePostcode = "B1 1AA",
                    Region = "West Midlands",
                    Skills = Array.Empty<string>(),
                    Rating = 4.5,
                    IsActive = true
                }
            ]);
        }
    }

    private sealed class StubOfficeRepository : IOfficeRepository
    {
        public Task<IReadOnlyList<Office>> GetAllAsync(CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<Office>>([
                new Office
                {
                    OfficeId = "OFF-1",
                    Name = "Bromsgrove",
                    Postcode = "B60 1AA",
                    Region = "West Midlands"
                }
            ]);
        }
    }

    private sealed class StubGeocodingService : IGeocodingService
    {
        public Task<(double Lat, double Lng)> GeocodeAsync(string address, CancellationToken ct) => Task.FromResult((0d, 0d));
    }

    private sealed class StubCoveragePolicyProvider : ICoveragePolicyProvider
    {
        public Task<CoveragePolicy> GetAsync(CancellationToken ct)
        {
            return Task.FromResult(new CoveragePolicy
            {
                DefaultRadiusMiles = 50,
                DefaultMaxTravelTimeMinutes = 90
            });
        }
    }

    private sealed class StubCoverageSettings : ICoveragePresentationSettings
    {
        public double AverageTravelSpeedMph => 35d;
    }
}
