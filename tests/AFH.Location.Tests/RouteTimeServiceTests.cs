using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Models.V1.Travel;
using AFH.Location.Application.Services.V1.Travel;
using AFH.Location.Domain.Travel;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFH.Location.Tests;

public sealed class RouteTimeServiceTests
{
    [Fact]
    public async Task CalculateAsync_ReturnsSucceeded_WhenMatrixReturnsRoute()
    {
        var matrix = new RecordingRouteMatrixService(new RouteResult(42, 18.5, "High", TravelRouteResolutionSource.AzureMaps));
        var sut = new RouteTimeService(matrix, NullLogger<RouteTimeService>.Instance);

        var result = await sut.CalculateAsync(Request(), CancellationToken.None);

        Assert.Equal(RouteTimeStatus.Succeeded, result.Status);
        Assert.Equal(42, result.TravelTimeMinutes);
        Assert.Equal(18.5, result.TravelDistanceMiles);
        Assert.Equal(1, matrix.OneToManyCallCount);
        Assert.NotNull(matrix.LastDepartAt);
        Assert.Single(matrix.LastDestinations!);
    }

    [Fact]
    public async Task CalculateAsync_ReturnsRouteUnavailable_WhenMatrixReturnsSyntheticFallback()
    {
        var matrix = new RecordingRouteMatrixService(new RouteResult(0, 0, "Low", TravelRouteResolutionSource.AzureMaps));
        var sut = new RouteTimeService(matrix, NullLogger<RouteTimeService>.Instance);

        var result = await sut.CalculateAsync(Request(), CancellationToken.None);

        Assert.Equal(RouteTimeStatus.RouteUnavailable, result.Status);
        Assert.Null(result.TravelTimeMinutes);
        Assert.Null(result.TravelDistanceMiles);
    }

    private static RouteTimeRequest Request()
        => new()
        {
            CorrelationId = "corr-1",
            DepartAt = new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero),
            Source = new LocationCoordinates(53.4794, -2.2453),
            Destination = new LocationCoordinates(51.5014, -0.1419)
        };

    private sealed class RecordingRouteMatrixService : IRouteMatrixService
    {
        private readonly RouteResult _result;

        public RecordingRouteMatrixService(RouteResult result)
        {
            _result = result;
        }

        public int OneToManyCallCount { get; private set; }
        public DateTimeOffset? LastDepartAt { get; private set; }
        public IReadOnlyDictionary<string, (double Lat, double Lng)>? LastDestinations { get; private set; }

        public Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
            IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
            (double Lat, double Lng) destination,
            CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
            (double Lat, double Lng) origin,
            IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
            DateTimeOffset? departAt = null,
            CancellationToken ct = default)
        {
            OneToManyCallCount++;
            LastDepartAt = departAt;
            LastDestinations = destinations;
            return Task.FromResult<IReadOnlyDictionary<string, RouteResult>>(
                destinations.ToDictionary(x => x.Key, _ => _result, StringComparer.OrdinalIgnoreCase));
        }
    }
}
