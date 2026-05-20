using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Abstractions.Travel;
using AFH.Location.Application.Models.V1.Travel;
using AFH.Location.Application.Services.V1.Travel;
using AFH.Location.Domain;
using AFH.Location.Domain.Travel;
using AFH.Location.Infrastructure.External.Maps;
using AFH.Location.Infrastructure.External.Maps.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using Xunit;

namespace AFH.Location.Tests;

public sealed class TravelCoverageServiceTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. ParseMatrix root-cause fix: 2D positional array
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AzureMapsRouteMatrixService_ParsesPositional2DMatrixArray_OneOriginOneDestination()
    {
        // Arrange — the official Azure Route Matrix response format:
        // matrix[originRow][destinationCol], no originIndex / destinationIndex on cells.
        const string responseBody = """
            {
              "formatVersion": "0.0.1",
              "matrix": [
                [
                  {
                    "statusCode": 200,
                    "response": {
                      "routeSummary": {
                        "lengthInMeters": 5134,
                        "travelTimeInSeconds": 720,
                        "trafficDelayInSeconds": 0
                      }
                    }
                  }
                ]
              ],
              "summary": { "successfulRoutes": 1, "totalRoutes": 1 }
            }
            """;

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Maps:Azure:Key"] = "test-key" })
            .Build();

        var sut = new AzureMapsRouteMatrixService(new StubHttpClientFactory(new HttpClient(handler)), cfg);

        // Act
        var results = await sut.GetOneToManyAsync(
            (51.73, 0.47),
            new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase) { ["dest-1"] = (51.74, 0.48) },
            CancellationToken.None);

        // Assert — the route must be populated, not the synthetic fallback (0, 0, "Low")
        Assert.True(results.TryGetValue("dest-1", out var route));
        Assert.Equal(12, route.EtaMinutes);   // ceil(720 / 60) = 12
        Assert.Equal(3.19, route.DistanceMiles);  // round(5134 / 1609.344, 2) = 3.19
        Assert.Equal("High", route.Confidence);
    }

    [Fact]
    public async Task AzureMapsRouteMatrixService_ParsesPositional2DMatrixArray_MultipleOrigins()
    {
        // 2 origins × 2 destinations (the example from the official Azure Maps docs)
        const string responseBody = """
            {
              "formatVersion": "0.0.1",
              "matrix": [
                [
                  { "statusCode": 200, "response": { "routeSummary": { "lengthInMeters": 495, "travelTimeInSeconds": 134 } } },
                  { "statusCode": 200, "response": { "routeSummary": { "lengthInMeters": 647651, "travelTimeInSeconds": 26835 } } }
                ],
                [
                  { "statusCode": 200, "response": { "routeSummary": { "lengthInMeters": 338, "travelTimeInSeconds": 104 } } },
                  { "statusCode": 200, "response": { "routeSummary": { "lengthInMeters": 647494, "travelTimeInSeconds": 26763 } } }
                ]
              ],
              "summary": { "successfulRoutes": 4, "totalRoutes": 4 }
            }
            """;

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Maps:Azure:Key"] = "test-key" })
            .Build();

        var sut = new AzureMapsRouteMatrixService(new StubHttpClientFactory(new HttpClient(handler)), cfg);

        // GetAdviserToDestination: 2 advisers → 1 destination
        var results = await sut.GetAdviserToDestinationAsync(
            new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
            {
                ["adv-1"] = (52.36006, 4.85106),
                ["adv-2"] = (52.36187, 4.85056)
            },
            (52.36241, 4.85003),
            CancellationToken.None);

        Assert.Equal(2, results.Count);

        // origin 0 (adv-1) → dest 0 (DEST): 134 s → ceil(134/60) = 3 min
        Assert.Equal(3, results["adv-1"].EtaMinutes);
        Assert.Equal("High", results["adv-1"].Confidence);

        // origin 1 (adv-2) → dest 0 (DEST): 104 s → ceil(104/60) = 2 min
        Assert.Equal(2, results["adv-2"].EtaMinutes);
        Assert.Equal("High", results["adv-2"].Confidence);
    }

    [Fact]
    public async Task AzureMapsRouteMatrixService_NonSuccessStatusCode_IsLowConfidence_NotHighConfidence()
    {
        const string responseBody = """
            {
              "matrix": [
                [
                  { "statusCode": 400 }
                ]
              ],
              "summary": { "successfulRoutes": 0, "totalRoutes": 1 }
            }
            """;

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Maps:Azure:Key"] = "test-key" })
            .Build();

        var sut = new AzureMapsRouteMatrixService(new StubHttpClientFactory(new HttpClient(handler)), cfg);

        var results = await sut.GetOneToManyAsync(
            (51.73, 0.47),
            new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase) { ["dest-1"] = (51.74, 0.48) },
            CancellationToken.None);

        Assert.True(results.TryGetValue("dest-1", out var route));
        Assert.Equal("Low", route.Confidence);
        Assert.Equal(0, route.EtaMinutes);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. CachedRouteMatrixService: synthetic fallback must not be cached
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CachedRouteMatrixService_DoesNotCacheSyntheticFallback_WhenProviderReturnsNoData()
    {
        // Arrange — inner always returns no data (simulates ParseMatrix returning empty map).
        var inner = new EmptyRouteMatrixService();
        var cache = new InspectableRouteCache();
        var sut = new CachedRouteMatrixService(inner, cache);

        // Act
        await sut.GetOneToManyAsync(
            (51.73, 0.47),
            new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase) { ["dest-1"] = (51.74, 0.48) },
            CancellationToken.None);

        // Assert — nothing must have been written to the cache
        Assert.Empty(cache.Written);
    }

    [Fact]
    public async Task CachedRouteMatrixService_CachesGenuineProviderResult()
    {
        var inner = new FixedRouteMatrixService(new RouteResult(12, 3.19, "High", TravelRouteResolutionSource.AzureMaps));
        var cache = new InspectableRouteCache();
        var sut = new CachedRouteMatrixService(inner, cache);

        await sut.GetOneToManyAsync(
            (51.73, 0.47),
            new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase) { ["dest-1"] = (51.74, 0.48) },
            CancellationToken.None);

        // Two entries are written: the id-specific key and the single-route key
        Assert.Equal(2, cache.Written.Count);
        Assert.All(cache.Written.Values, r => Assert.Equal("High", r.Confidence));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. TravelRouteOutcomeProvider: zero ETA/distance treated as usable (>= 0)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TravelRouteOutcomeProvider_MapsZeroEtaAndDistanceAsUsable()
    {
        var matrixService = new StubRouteMatrixService(
            new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase)
            {
                ["dest-1"] = new(0, 0d, "High", TravelRouteResolutionSource.AzureMaps)
            });
        var policyProvider = new StubRouteMatrixPolicyProvider();
        var sut = new TravelRouteOutcomeProvider(matrixService, policyProvider);

        var request = new TravelRouteOutcomeRequest
        {
            Source = new(51.73, 0.47),
            Destinations = new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase)
            {
                ["dest-1"] = new(51.73, 0.47)
            }
        };

        var outcomes = await sut.GetOutcomesAsync(request, CancellationToken.None);

        Assert.True(outcomes.TryGetValue("dest-1", out var outcome));
        Assert.True(outcome.HasUsableRoute);
        Assert.Equal(0, outcome.TravelTimeMinutes);
        Assert.Equal(0d, outcome.DistanceMiles);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Geocoding: no idxSet=PAD in request URL
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AzureMapsGeocodingService_GeocodeAsync_DoesNotIncludePadIndexSet()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"results\":[{\"position\":{\"lat\":51.731,\"lon\":0.468}}]}",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Maps:Azure:Key"] = "test-key" })
            .Build();

        var sut = new AzureMapsGeocodingService(new StubHttpClientFactory(new HttpClient(handler)), configuration);
        var result = await sut.GeocodeAsync("CM1 2FG", CancellationToken.None);

        Assert.NotNull(captured);
        var uriString = captured!.RequestUri?.ToString();
        Assert.NotNull(uriString);
        Assert.Contains("query=CM1", uriString);
        Assert.DoesNotContain("idxSet=PAD", uriString);
        Assert.DoesNotContain("idxSet=", uriString);
        Assert.Equal(51.731, result.Lat);
        Assert.Equal(0.468, result.Lng);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. TravelCoverageService: nearby postcodes resolve to route and coverage
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_NearbyPostcodes_ProducesUsableRouteAndCoverage()
    {
        var resolver = new StubPostcodeResolver(new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase)
        {
            ["CM1 2FG"] = new(51.73, 0.47),
            ["CM1 2GG"] = new(51.74, 0.48)
        });

        // Simulate a real provider response (as would happen after ParseMatrix fix)
        var provider = new RecordingRouteOutcomeProvider(
            new Dictionary<string, TravelRouteOutcome>(StringComparer.OrdinalIgnoreCase)
            {
                ["dest-1"] = new(8, 2.1, "High", TravelRouteResolutionSource.AzureMaps)
            });

        var sut = new TravelCoverageService(resolver, provider, NullLogger<TravelCoverageService>.Instance);

        var request = new TravelCoverageRequest
        {
            SourcePostcode = "CM1 2FG",
            Destinations =
            [
                new TravelCoverageDestinationRequest
                {
                    CorrelationId = "dest-1",
                    Postcode = "CM1 2GG",
                    MaxTravelTimeMinutes = 30,
                    MaxDistanceMiles = 10.0
                }
            ]
        };

        var result = await sut.EvaluateAsync(request, CancellationToken.None);

        Assert.Single(result.Destinations);
        var outcome = result.Destinations[0];

        Assert.Equal(TravelCoverageStatus.Succeeded, outcome.Status);
        Assert.NotNull(outcome.Route);
        Assert.Equal(8, outcome.Route!.TravelTimeMinutes);
        Assert.Equal(2.1, outcome.Route.DistanceMiles);
        Assert.NotNull(outcome.Coverage);
        Assert.True(outcome.Coverage!.IsWithinCoverage);
        Assert.Equal(1, provider.CallCount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Lean API Response Mapping & Slot Generation Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LocationContractMapper_ToContractResponse_WithTimeIndependentWindow_GeneratesCorrectSlots()
    {
        // Arrange
        var startTime = DateTimeOffset.Parse("2026-05-20T08:00:00Z");
        var endTime = DateTimeOffset.Parse("2026-05-20T10:00:00Z");

        var result = new TravelCoverageResult
        {
            SourcePostcode = "CM1 2FG",
            SourceCoordinates = new LocationCoordinates(51.73, 0.47),
            TimeContext = new TravelCoverageTimeContext
            {
                TimingMode = TravelCoverageTimingMode.TimeIndependent,
                StartTime = startTime,
                EndTime = endTime,
                SearchIntervalMinutes = 30
            },
            RequestContext = new LocationRequestContext { CorrelationId = "c-123", RequestedBy = "test-user" },
            Destinations =
            [
                new TravelCoverageDestinationOutcome
                {
                    CorrelationId = "dest-1",
                    Postcode = "CM1 2GG",
                    Status = TravelCoverageStatus.Succeeded,
                    Coordinates = new LocationCoordinates(51.74, 0.48),
                    Route = new TravelRouteOutcome(12, 3.5, "High", TravelRouteResolutionSource.AzureMaps),
                    Coverage = new TravelCoverageOutcome
                    {
                        IsWithinCoverage = true,
                        MaxTravelTimeMinutes = 30,
                        MaxDistanceMiles = 10.0
                    }
                }
            ]
        };

        // Act
        var contractResponse = AFH.Location.Function.Mapping.V1.LocationContractMapper.ToContractResponse(result);

        // Assert
        Assert.NotNull(contractResponse);
        Assert.Equal("CM1 2FG", contractResponse.SourcePostcode);
        Assert.Equal("c-123", contractResponse.RequestContext.CorrelationId);
        
        Assert.Single(contractResponse.Destinations);
        var destOutcome = contractResponse.Destinations[0];
        Assert.Equal("dest-1", destOutcome.CorrelationId);
        Assert.Equal("CM1 2GG", destOutcome.Postcode);
        Assert.Equal(AFH.Location.Contract.V1.Responses.Travel.TravelCoverageStatusV1.Succeeded, destOutcome.Status);
        
        Assert.NotNull(destOutcome.Slots);
        Assert.Equal(4, destOutcome.Slots.Count); // (10:00 - 08:00) = 2 hours / 30 mins = 4 slots
        
        Assert.All(destOutcome.Slots, slot =>
        {
            Assert.Equal(12, slot.TravelTimeMinutes);
            Assert.Equal(3.5, slot.TravelDistanceMiles);
            Assert.True(slot.IsWithinCoverage);
        });

        Assert.Equal(startTime, destOutcome.Slots[0].StartTime);
        Assert.Equal(startTime.AddMinutes(30), destOutcome.Slots[0].EndTime);

        Assert.Equal(endTime.AddMinutes(-30), destOutcome.Slots[3].StartTime);
        Assert.Equal(endTime, destOutcome.Slots[3].EndTime);
    }

    [Fact]
    public void LocationContractMapper_ToContractResponse_OnFailure_HasNullSlotsAndPopulatedWarnings()
    {
        // Arrange
        var result = new TravelCoverageResult
        {
            SourcePostcode = "CM1 2FG",
            TimeContext = new TravelCoverageTimeContext
            {
                TimingMode = TravelCoverageTimingMode.TimeIndependent
            },
            RequestContext = new LocationRequestContext { CorrelationId = "c-123" },
            Destinations =
            [
                new TravelCoverageDestinationOutcome
                {
                    CorrelationId = "dest-1",
                    Postcode = "CM1 2GG",
                    Status = TravelCoverageStatus.DestinationPostcodeUnresolved,
                    Warnings = [new TravelCoverageWarning("DESTINATION_POSTCODE_UNRESOLVED", "Unresolved")]
                }
            ]
        };

        // Act
        var contractResponse = AFH.Location.Function.Mapping.V1.LocationContractMapper.ToContractResponse(result);

        // Assert
        Assert.NotNull(contractResponse);
        Assert.Single(contractResponse.Destinations);
        var destOutcome = contractResponse.Destinations[0];
        Assert.Equal(AFH.Location.Contract.V1.Responses.Travel.TravelCoverageStatusV1.DestinationPostcodeUnresolved, destOutcome.Status);
        Assert.Null(destOutcome.Slots);
        Assert.NotNull(destOutcome.Warnings);
        Assert.Single(destOutcome.Warnings);
        Assert.Equal("DESTINATION_POSTCODE_UNRESOLVED", destOutcome.Warnings[0].Code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test doubles
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handle = handle;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handle(request));
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        private readonly HttpClient _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubPostcodeResolver(IReadOnlyDictionary<string, LocationCoordinates> coordinates)
        : IPostcodeCoordinateResolver
    {
        private readonly IReadOnlyDictionary<string, LocationCoordinates> _coordinates = coordinates;

        public Task<PostcodeCoordinateResolution> ResolveAsync(string postcode, CancellationToken ct)
        {
            var normalised = string.Join(" ", postcode.Trim().ToUpperInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
            _coordinates.TryGetValue(normalised, out var coords);
            return Task.FromResult(new PostcodeCoordinateResolution { Postcode = postcode, Coordinates = coords });
        }
    }

    private sealed class RecordingRouteOutcomeProvider : ITravelRouteOutcomeProvider
    {
        private readonly IReadOnlyDictionary<string, TravelRouteOutcome> _predefined;
        public int CallCount { get; private set; }

        public RecordingRouteOutcomeProvider(IReadOnlyDictionary<string, TravelRouteOutcome>? predefined = null)
        {
            _predefined = predefined ?? new Dictionary<string, TravelRouteOutcome>(StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyDictionary<string, TravelRouteOutcome>> GetOutcomesAsync(
            TravelRouteOutcomeRequest request, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(_predefined);
        }
    }

    private sealed class StubRouteMatrixService(IReadOnlyDictionary<string, RouteResult> results)
        : IRouteMatrixService
    {
        private readonly IReadOnlyDictionary<string, RouteResult> _results = results;

        public Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
            IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
            (double Lat, double Lng) destination,
            CancellationToken ct)
            => throw new NotImplementedException();

        public Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
            (double Lat, double Lng) origin,
            IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
            CancellationToken ct)
            => Task.FromResult(_results);
    }

    /// <summary>Returns an empty result for every request — simulates ParseMatrix bug.</summary>
    private sealed class EmptyRouteMatrixService : IRouteMatrixService
    {
        public Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
            IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
            (double Lat, double Lng) destination,
            CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, RouteResult>>(
                new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase));

        public Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
            (double Lat, double Lng) origin,
            IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
            CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, RouteResult>>(
                new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Always returns a fixed RouteResult for every destination key.</summary>
    private sealed class FixedRouteMatrixService(RouteResult result) : IRouteMatrixService
    {
        private readonly RouteResult _result = result;

        public Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
            IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
            (double Lat, double Lng) destination,
            CancellationToken ct)
            => throw new NotImplementedException();

        public Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
            (double Lat, double Lng) origin,
            IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
            CancellationToken ct)
        {
            IReadOnlyDictionary<string, RouteResult> result = destinations.ToDictionary(
                kv => kv.Key, _ => _result, StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }
    }

    /// <summary>A route cache that records every Set call for assertion.</summary>
    private sealed class InspectableRouteCache : IRouteCache
    {
        public Dictionary<string, RouteResult> Written { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool TryGet(string key, out RouteResult result)
        {
            result = default!;
            return false;
        }

        public void Set(string key, RouteResult result, TimeSpan ttl) => Written[key] = result;
    }

    private sealed class StubRouteMatrixPolicyProvider : IRouteMatrixPolicyProvider
    {
        public Task<RouteMatrixPolicy> GetAsync(CancellationToken ct)
            => Task.FromResult(new RouteMatrixPolicy { MaxDestinationsPerCall = 100 });
    }
}
