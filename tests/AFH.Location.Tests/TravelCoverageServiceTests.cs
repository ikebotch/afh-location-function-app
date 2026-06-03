using AFH.Location.Application.Services.Travel;
using AFH.Location.Application.Validation.Travel;
using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Application.Abstractions.Travel;
using AFH.Location.Application.Models.Travel;
using AFH.Location.Contract.V1.Requests.Travel;
using AFH.Location.Contract.V1.Responses.Travel;
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
            ct: CancellationToken.None);

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
            ct: CancellationToken.None);

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
            ct: CancellationToken.None);

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
            ct: CancellationToken.None);

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

    [Fact]
    public async Task EvaluateAsync_ProximatePostcodes_UsesFallback_WhenProviderReturnsSyntheticNoRoute()
    {
        var resolver = new StubPostcodeResolver(new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase)
        {
            ["AB1 1AA"] = new(53.381100, -1.470100),
            ["AB1 1AB"] = new(53.381300, -1.470100)
        });

        var matrix = new StubRouteMatrixService(
            new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase)
            {
                ["adv-1"] = new(0, 0d, "Low", TravelRouteResolutionSource.AzureMaps)
            });
        var provider = new TravelRouteOutcomeProvider(matrix, new StubRouteMatrixPolicyProvider());
        var sut = new TravelCoverageService(resolver, provider, NullLogger<TravelCoverageService>.Instance);

        var request = new TravelCoverageRequest
        {
            SourcePostcode = "AB1 1AA",
            Destinations =
            [
                new TravelCoverageDestinationRequest
                {
                    CorrelationId = "adv-1",
                    Postcode = "AB1 1AB",
                    MaxTravelTimeMinutes = 30,
                    MaxDistanceMiles = 10.0
                }
            ]
        };

        var result = await sut.EvaluateAsync(request, CancellationToken.None);

        var outcome = Assert.Single(result.Destinations);
        Assert.Equal(TravelCoverageStatus.Succeeded, outcome.Status);
        Assert.NotNull(outcome.Route);
        Assert.Equal(1, outcome.Route!.TravelTimeMinutes);
        Assert.True(outcome.Route.DistanceMiles is > 0 and <= 0.1);
        Assert.Equal("High", outcome.Route.Confidence);
        Assert.NotNull(outcome.Coverage);
        Assert.True(outcome.Coverage!.IsWithinCoverage);
    }

    [Fact]
    public async Task EvaluateAsync_UnresolvedDestinationPostcode_DoesNotUseProximateFallback()
    {
        var resolver = new StubPostcodeResolver(new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase)
        {
            ["AB1 1AA"] = new(53.381100, -1.470100)
        });

        var provider = new RecordingRouteOutcomeProvider();
        var sut = new TravelCoverageService(resolver, provider, NullLogger<TravelCoverageService>.Instance);

        var request = new TravelCoverageRequest
        {
            SourcePostcode = "AB1 1AA",
            Destinations =
            [
                new TravelCoverageDestinationRequest
                {
                    CorrelationId = "adv-1",
                    Postcode = "NOT A POSTCODE",
                    MaxTravelTimeMinutes = 30,
                    MaxDistanceMiles = 10.0
                }
            ]
        };

        var result = await sut.EvaluateAsync(request, CancellationToken.None);

        var outcome = Assert.Single(result.Destinations);
        Assert.Equal(TravelCoverageStatus.DestinationPostcodeUnresolved, outcome.Status);
        Assert.Null(outcome.Route);
        Assert.Null(outcome.Coverage);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_SamePostcode_ShortCircuitsToZeroWithoutCallingProvider()
    {
        var resolver = new StubPostcodeResolver(new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase)
        {
            ["CM1 2FG"] = new(51.73, 0.47)
        });

        var provider = new RecordingRouteOutcomeProvider();
        var sut = new TravelCoverageService(resolver, provider, NullLogger<TravelCoverageService>.Instance);

        var request = new TravelCoverageRequest
        {
            SourcePostcode = "CM1 2FG",
            Destinations =
            [
                new TravelCoverageDestinationRequest
                {
                    CorrelationId = "dest-1",
                    Postcode = "CM1 2FG",
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
        Assert.Equal(0, outcome.Route!.TravelTimeMinutes);
        Assert.Equal(0d, outcome.Route.DistanceMiles);
        Assert.NotNull(outcome.Coverage);
        Assert.True(outcome.Coverage!.IsWithinCoverage);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_SameCoordinates_ShortCircuitsToZeroWithoutCallingProvider()
    {
        var resolver = new StubPostcodeResolver(new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase)
        {
            ["CM1 2FG"] = new(51.73, 0.47),
            ["CM1 2GG"] = new(51.73, 0.47) // Same coordinates
        });

        var provider = new RecordingRouteOutcomeProvider();
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
        Assert.Equal(0, outcome.Route!.TravelTimeMinutes);
        Assert.Equal(0d, outcome.Route.DistanceMiles);
        Assert.NotNull(outcome.Coverage);
        Assert.True(outcome.Coverage!.IsWithinCoverage);
        Assert.Equal(0, provider.CallCount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Lean API Response Mapping & Slot Generation Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TravelCoverageContractMapper_ToContractResponse_WithTimeIndependentWindow_GeneratesCorrectSlots()
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
                    },
                    PresentedSlots =
                    [
                        new TravelCoveragePresentedSlot
                        {
                            StartTime = startTime,
                            EndTime = endTime,
                            TravelTimeMinutes = 12,
                            TravelDistanceMiles = 3.5,
                            IsWithinCoverage = true
                        }
                    ]
                }
            ]
        };

        // Act
        var contractResponse = AFH.Location.Function.Mapping.V1.Location.TravelCoverageContractMapper.ToContractResponse(result);

        // Assert
        Assert.NotNull(contractResponse);
        Assert.Equal("CM1 2FG", contractResponse.SourcePostcode);
        Assert.Equal("c-123", contractResponse.RequestContext.CorrelationId);
        Assert.Equal(AFH.Location.Contract.V1.Requests.Travel.TravelEvaluationModeV1.TimeIndependent, contractResponse.TimeContext.TravelEvaluationMode);
        Assert.Equal(AFH.Location.Contract.V1.Requests.Travel.SlotResponseModeV1.Grouped, contractResponse.TimeContext.SlotResponseMode);
        
        Assert.Single(contractResponse.Destinations);
        var destOutcome = contractResponse.Destinations[0];
        Assert.Equal("dest-1", destOutcome.CorrelationId);
        Assert.Equal("CM1 2GG", destOutcome.Postcode);
        Assert.Equal(AFH.Location.Contract.V1.Responses.Travel.TravelCoverageStatusV1.Succeeded, destOutcome.Status);
        
        Assert.NotNull(destOutcome.Slots);
        var singleSlot = Assert.Single(destOutcome.Slots);
        Assert.Equal(startTime, singleSlot.StartTime);
        Assert.Equal(endTime, singleSlot.EndTime);
        Assert.Equal(12, singleSlot.TravelTimeMinutes);
        Assert.Equal(3.5, singleSlot.TravelDistanceMiles);
        Assert.True(singleSlot.IsWithinCoverage);
    }

    [Fact]
    public void TravelCoverageContractMapper_ToContractResponse_OnFailure_HasNullSlotsAndPopulatedWarnings()
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
        var contractResponse = AFH.Location.Function.Mapping.V1.Location.TravelCoverageContractMapper.ToContractResponse(result);

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

    [Fact]
    public void TravelCoverageRequestV1_DeserializesEnumValuesCaseInsensitively()
    {
        // Arrange — mix of all lowercase, camelCase, and PascalCase string representations of enums
        const string json1 = """
            {
              "sourcePostcode": "CM1 2FG",
              "timeContext": {
                "travelEvaluationMode": "timeindependent",
                "slotResponseMode": "grouped"
              }
            }
            """;

        const string json2 = """
            {
              "sourcePostcode": "CM1 2FG",
              "timeContext": {
                "travelEvaluationMode": "timeDependent",
                "slotResponseMode": "expanded"
              }
            }
            """;

        const string json3 = """
            {
              "sourcePostcode": "CM1 2FG",
              "timeContext": {
                "travelEvaluationMode": "TIMEDEPENDENT",
                "slotResponseMode": "summary"
              }
            }
            """;

        // Act
        var req1 = System.Text.Json.JsonSerializer.Deserialize<TravelCoverageRequestV1>(json1, new System.Text.Json.JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });
        var req2 = System.Text.Json.JsonSerializer.Deserialize<TravelCoverageRequestV1>(json2, new System.Text.Json.JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });
        var req3 = System.Text.Json.JsonSerializer.Deserialize<TravelCoverageRequestV1>(json3, new System.Text.Json.JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        // Assert
        Assert.NotNull(req1);
        Assert.Equal(AFH.Location.Contract.V1.Requests.Travel.TravelEvaluationModeV1.TimeIndependent, req1.TimeContext.TravelEvaluationMode);
        Assert.Equal(AFH.Location.Contract.V1.Requests.Travel.SlotResponseModeV1.Grouped, req1.TimeContext.SlotResponseMode);

        Assert.NotNull(req2);
        Assert.Equal(AFH.Location.Contract.V1.Requests.Travel.TravelEvaluationModeV1.TimeDependent, req2.TimeContext.TravelEvaluationMode);
        Assert.Equal(AFH.Location.Contract.V1.Requests.Travel.SlotResponseModeV1.Expanded, req2.TimeContext.SlotResponseMode);

        Assert.NotNull(req3);
        Assert.Equal(AFH.Location.Contract.V1.Requests.Travel.TravelEvaluationModeV1.TimeDependent, req3.TimeContext.TravelEvaluationMode);
        Assert.Equal(AFH.Location.Contract.V1.Requests.Travel.SlotResponseModeV1.Summary, req3.TimeContext.SlotResponseMode);
    }

    [Fact]
    public async Task EvaluateAsync_TimeDependent_HonoursSlotResponseModesAndReturnsEmptyWarningsOnSuccess()
    {
        // Arrange
        var resolver = new StubPostcodeResolver(new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase)
        {
            ["CM1 2FG"] = new(51.73, 0.47),
            ["CM1 2GG"] = new(51.74, 0.48)
        });

        // Set up provider that returns varying ETA based on departure time:
        // 08:00 -> ETA 45 mins
        // 08:30 -> ETA 45 mins
        // 09:00 -> ETA 65 mins (rush hour)
        var provider = new TimeDependentRecordingRouteOutcomeProvider();
        var service = new TravelCoverageService(resolver, provider, NullLogger<TravelCoverageService>.Instance);

        var request = new TravelCoverageRequest
        {
            SourcePostcode = "CM1 2FG",
            TimeContext = new TravelCoverageTimeContext
            {
                TimingMode = TravelCoverageTimingMode.DepartureTime,
                StartTime = DateTimeOffset.Parse("2026-05-22T08:00:00Z"),
                EndTime = DateTimeOffset.Parse("2026-05-22T09:30:00Z"),
                SearchIntervalMinutes = 30
            },
            Destinations =
            [
                new TravelCoverageDestinationRequest
                {
                    CorrelationId = "dest-1",
                    Postcode = "CM1 2GG",
                    MaxTravelTimeMinutes = 60,
                    MaxDistanceMiles = 30.0
                }
            ]
        };

        // Act & Assert 1: Expanded Mode
        var requestExpanded = request with { TimeContext = request.TimeContext with { SlotResponseMode = TravelCoverageSlotResponseMode.Expanded } };
        var resultExpanded = await service.EvaluateAsync(requestExpanded, CancellationToken.None);
        var responseExpanded = AFH.Location.Function.Mapping.V1.Location.TravelCoverageContractMapper.ToContractResponse(resultExpanded);

        Assert.NotNull(responseExpanded);
        Assert.Single(responseExpanded.Destinations);
        var destExpanded = responseExpanded.Destinations[0];
        Assert.Equal(AFH.Location.Contract.V1.Responses.Travel.TravelCoverageStatusV1.Succeeded, destExpanded.Status);
        Assert.NotNull(destExpanded.Warnings);
        Assert.Empty(destExpanded.Warnings); // Verify Warnings is [] instead of null

        Assert.NotNull(destExpanded.Slots);
        Assert.Equal(3, destExpanded.Slots.Count);

        // Slot 1: 08:00 - 08:30, ETA 45, within coverage
        Assert.Equal(DateTimeOffset.Parse("2026-05-22T08:00:00Z"), destExpanded.Slots[0].StartTime);
        Assert.Equal(DateTimeOffset.Parse("2026-05-22T08:30:00Z"), destExpanded.Slots[0].EndTime);
        Assert.Equal(45, destExpanded.Slots[0].TravelTimeMinutes);
        Assert.True(destExpanded.Slots[0].IsWithinCoverage);

        // Slot 2: 08:30 - 09:00, ETA 45, within coverage
        Assert.Equal(DateTimeOffset.Parse("2026-05-22T08:30:00Z"), destExpanded.Slots[1].StartTime);
        Assert.Equal(DateTimeOffset.Parse("2026-05-22T09:00:00Z"), destExpanded.Slots[1].EndTime);
        Assert.Equal(45, destExpanded.Slots[1].TravelTimeMinutes);
        Assert.True(destExpanded.Slots[1].IsWithinCoverage);

        // Slot 3: 09:00 - 09:30, ETA 65, NOT within coverage (exceeds 60 mins limit)
        Assert.Equal(DateTimeOffset.Parse("2026-05-22T09:00:00Z"), destExpanded.Slots[2].StartTime);
        Assert.Equal(DateTimeOffset.Parse("2026-05-22T09:30:00Z"), destExpanded.Slots[2].EndTime);
        Assert.Equal(65, destExpanded.Slots[2].TravelTimeMinutes);
        Assert.False(destExpanded.Slots[2].IsWithinCoverage);


        // Act & Assert 2: Grouped Mode (default / requested)
        var requestGrouped = request with { TimeContext = request.TimeContext with { SlotResponseMode = TravelCoverageSlotResponseMode.Grouped } };
        var resultGrouped = await service.EvaluateAsync(requestGrouped, CancellationToken.None);
        var responseGrouped = AFH.Location.Function.Mapping.V1.Location.TravelCoverageContractMapper.ToContractResponse(resultGrouped);

        Assert.NotNull(responseGrouped);
        var destGrouped = responseGrouped.Destinations[0];
        Assert.NotNull(destGrouped.Slots);
        
        // Slot 1 and Slot 2 are adjacent and identical, so they merge!
        // Slot 3 has different travel time/coverage, so it remains separate.
        Assert.Equal(2, destGrouped.Slots.Count);

        // Merged Slot 1: 08:00 - 09:00, ETA 45, within coverage
        Assert.Equal(DateTimeOffset.Parse("2026-05-22T08:00:00Z"), destGrouped.Slots[0].StartTime);
        Assert.Equal(DateTimeOffset.Parse("2026-05-22T09:00:00Z"), destGrouped.Slots[0].EndTime);
        Assert.Equal(45, destGrouped.Slots[0].TravelTimeMinutes);
        Assert.True(destGrouped.Slots[0].IsWithinCoverage);

        // Separate Slot 2: 09:00 - 09:30, ETA 65, NOT within coverage
        Assert.Equal(DateTimeOffset.Parse("2026-05-22T09:00:00Z"), destGrouped.Slots[1].StartTime);
        Assert.Equal(DateTimeOffset.Parse("2026-05-22T09:30:00Z"), destGrouped.Slots[1].EndTime);
        Assert.Equal(65, destGrouped.Slots[1].TravelTimeMinutes);
        Assert.False(destGrouped.Slots[1].IsWithinCoverage);


        // Act & Assert 3: Summary Mode
        var requestSummary = request with { TimeContext = request.TimeContext with { SlotResponseMode = TravelCoverageSlotResponseMode.Summary } };
        var resultSummary = await service.EvaluateAsync(requestSummary, CancellationToken.None);
        var responseSummary = AFH.Location.Function.Mapping.V1.Location.TravelCoverageContractMapper.ToContractResponse(resultSummary);

        Assert.NotNull(responseSummary);
        var destSummary = responseSummary.Destinations[0];
        Assert.NotNull(destSummary.Slots);
        
        // Single rolled up slot
        var slotSummary = Assert.Single(destSummary.Slots);
        Assert.Equal(DateTimeOffset.Parse("2026-05-22T08:00:00Z"), slotSummary.StartTime);
        Assert.Equal(DateTimeOffset.Parse("2026-05-22T09:30:00Z"), slotSummary.EndTime);
        Assert.Equal(65, slotSummary.TravelTimeMinutes); // worst-case ETA
        Assert.Equal(25.5, slotSummary.TravelDistanceMiles);
        Assert.False(slotSummary.IsWithinCoverage); // not all intervals are within coverage
    }

    private sealed class TimeDependentRecordingRouteOutcomeProvider : ITravelRouteOutcomeProvider
    {
        public Task<IReadOnlyDictionary<string, TravelRouteOutcome>> GetOutcomesAsync(
            TravelRouteOutcomeRequest request, CancellationToken ct)
        {
            var results = new Dictionary<string, TravelRouteOutcome>(StringComparer.OrdinalIgnoreCase);
            var depart = request.TimeContext?.RequestedDepartureTime;
            
            int eta = 45;
            if (depart.HasValue && depart.Value.Hour == 9)
            {
                eta = 65; // rush hour!
            }

            results["dest-1"] = new TravelRouteOutcome(eta, 25.5, "High", TravelRouteResolutionSource.AzureMaps);
            return Task.FromResult<IReadOnlyDictionary<string, TravelRouteOutcome>>(results);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Performance Optimization, Bounded Parallelism, Max Slots & Cache Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TravelCoverageRequestValidator_RejectsOverlyLargeSlotRange_Above24Slots()
    {
        // Arrange
        var request = new TravelCoverageRequest
        {
            SourcePostcode = "CM1 2FG",
            TimeContext = new TravelCoverageTimeContext
            {
                StartTime = DateTimeOffset.Parse("2026-05-22T08:00:00Z"),
                EndTime = DateTimeOffset.Parse("2026-05-22T21:00:00Z"), // 13 hours
                SearchIntervalMinutes = 30, // 26 slots
                TimingMode = TravelCoverageTimingMode.DepartureTime,
                SlotResponseMode = TravelCoverageSlotResponseMode.Expanded
            },
            Destinations = new List<TravelCoverageDestinationRequest>
            {
                new TravelCoverageDestinationRequest
                {
                    CorrelationId = "dest-1",
                    Postcode = "CM1 2GG",
                    MaxTravelTimeMinutes = 60,
                    MaxDistanceMiles = 30.0
                }
            },
            RequestContext = new LocationRequestContext { CorrelationId = "req-1" }
        };

        // Act
        var errors = TravelCoverageRequestValidator.Validate(request);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("The requested time range generates too many slots. Maximum allowed is 24 slots."));
    }

    [Fact]
    public async Task TravelCoverageService_ExecutesTimeDependentSlotsConcurrentlyWithBoundedParallelism_AndPreservesDeterministicOrdering()
    {
        // Arrange
        var resolver = new StubPostcodeResolver(new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase)
        {
            ["CM1 2FG"] = new LocationCoordinates(51.73, 0.47),
            ["CM1 2GG"] = new LocationCoordinates(51.74, 0.48)
        });

        var delayProvider = new DelayingRouteOutcomeProvider();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TravelCoverage:MaxDegreeOfParallelism"] = "2",
                ["TravelCoverage:MaxGeneratedSlots"] = "24"
            })
            .Build();

        var service = new TravelCoverageService(resolver, delayProvider, NullLogger<TravelCoverageService>.Instance, configuration);

        var request = new TravelCoverageRequest
        {
            SourcePostcode = "CM1 2FG",
            TimeContext = new TravelCoverageTimeContext
            {
                StartTime = DateTimeOffset.Parse("2026-05-22T08:00:00Z"),
                EndTime = DateTimeOffset.Parse("2026-05-22T12:00:00Z"), // 4 hours -> 8 slots
                SearchIntervalMinutes = 30,
                TimingMode = TravelCoverageTimingMode.DepartureTime,
                SlotResponseMode = TravelCoverageSlotResponseMode.Expanded
            },
            Destinations = new List<TravelCoverageDestinationRequest>
            {
                new TravelCoverageDestinationRequest
                {
                    CorrelationId = "dest-1",
                    Postcode = "CM1 2GG",
                    MaxTravelTimeMinutes = 60,
                    MaxDistanceMiles = 30.0
                }
            },
            RequestContext = new LocationRequestContext { CorrelationId = "req-1" }
        };

        // Act
        var result = await service.EvaluateAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Destinations);
        
        var dest = result.Destinations[0];
        Assert.Equal(8, dest.Slots.Count);

        // Assert ordering is deterministic and sequential by StartTime
        var currentExpectedStart = DateTimeOffset.Parse("2026-05-22T08:00:00Z");
        for (int i = 0; i < dest.Slots.Count; i++)
        {
            var slot = dest.Slots[i];
            Assert.Equal(currentExpectedStart, slot.StartTime);
            currentExpectedStart = currentExpectedStart.AddMinutes(30);
        }

        // Assert that the max concurrency did not exceed the configured limit of 2
        Assert.True(delayProvider.MaxConcurrentCalls > 0);
        Assert.True(delayProvider.MaxConcurrentCalls <= 2, $"Concurrency exceeded: {delayProvider.MaxConcurrentCalls}");
    }

    [Fact]
    public async Task TravelCoverageService_TimeDependentStableWindow_EvaluatesOnlyAnchorSlots()
    {
        var resolver = new StubPostcodeResolver(new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase)
        {
            ["CM1 2FG"] = new(51.73, 0.47),
            ["CM1 2GG"] = new(51.74, 0.48)
        });
        var provider = new StableTimeDependentRouteOutcomeProvider(etaMinutes: 45, distanceMiles: 25.5);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TravelCoverage:AdaptiveAnchors:TravelTimeToleranceMinutes"] = "5",
                ["TravelCoverage:AdaptiveAnchors:DistanceToleranceMiles"] = "2",
                ["TravelCoverage:AdaptiveAnchors:CoverageThresholdBuffer"] = "5",
                ["TravelCoverage:AdaptiveAnchors:MaxExpansionDepth"] = "2",
                ["TravelCoverage:AdaptiveAnchors:MaxEvaluatedSlots"] = "8"
            })
            .Build();

        var service = new TravelCoverageService(resolver, provider, NullLogger<TravelCoverageService>.Instance, configuration);

        var request = BuildTimeDependentRequest(
            DateTimeOffset.Parse("2026-05-22T08:00:00Z"),
            DateTimeOffset.Parse("2026-05-22T17:00:00Z"),
            maxTravelTimeMinutes: 90,
            maxDistanceMiles: 60);

        var result = await service.EvaluateAsync(request, CancellationToken.None);

        var destination = Assert.Single(result.Destinations);
        Assert.Equal(18, destination.Slots.Count);
        Assert.Equal(3, provider.CallCount);
        Assert.Equal(
            [
                DateTimeOffset.Parse("2026-05-22T08:00:00Z"),
                DateTimeOffset.Parse("2026-05-22T12:30:00Z"),
                DateTimeOffset.Parse("2026-05-22T16:30:00Z")
            ],
            provider.Departures);
    }

    [Fact]
    public async Task TravelCoverageService_TimeDependentNearCoverageThreshold_ExpandsBeyondAnchorSlots()
    {
        var resolver = new StubPostcodeResolver(new Dictionary<string, LocationCoordinates>(StringComparer.OrdinalIgnoreCase)
        {
            ["CM1 2FG"] = new(51.73, 0.47),
            ["CM1 2GG"] = new(51.74, 0.48)
        });
        var provider = new StableTimeDependentRouteOutcomeProvider(etaMinutes: 45, distanceMiles: 25.5);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TravelCoverage:AdaptiveAnchors:CoverageThresholdBuffer"] = "5",
                ["TravelCoverage:AdaptiveAnchors:MaxExpansionDepth"] = "2",
                ["TravelCoverage:AdaptiveAnchors:MaxEvaluatedSlots"] = "5"
            })
            .Build();

        var service = new TravelCoverageService(resolver, provider, NullLogger<TravelCoverageService>.Instance, configuration);

        var request = BuildTimeDependentRequest(
            DateTimeOffset.Parse("2026-05-22T08:00:00Z"),
            DateTimeOffset.Parse("2026-05-22T17:00:00Z"),
            maxTravelTimeMinutes: 47,
            maxDistanceMiles: 60);

        var result = await service.EvaluateAsync(request, CancellationToken.None);

        var destination = Assert.Single(result.Destinations);
        Assert.Equal(18, destination.Slots.Count);
        Assert.True(provider.CallCount > 3);
        Assert.True(provider.CallCount <= 5);
    }

    [Fact]
    public async Task CachedRouteMatrixService_UsesRequestScopedInMemoryCache_ToAvoidRepeatedPersistentCacheHits()
    {
        // Arrange
        var mockInner = new FixedRouteMatrixService(new RouteResult(15, 10.0, "High", TravelRouteResolutionSource.AzureMaps));
        var mockPersistentCache = new CountingRouteCache();
        var sut = new CachedRouteMatrixService(mockInner, mockPersistentCache);

        var origin = (51.73, 0.47);
        var destinations = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
        {
            ["dest-1"] = (51.74, 0.48)
        };
        var departAt = DateTimeOffset.Parse("2026-05-22T08:00:00Z");

        // Act - Call 1 (misses cache, queries inner, caches to persistent cache and request cache)
        var result1 = await sut.GetOneToManyAsync(origin, destinations, departAt, CancellationToken.None);

        // Act - Call 2 (in the same request context, should hit the scoped request cache directly, NOT querying persistent cache again!)
        var result2 = await sut.GetOneToManyAsync(origin, destinations, departAt, CancellationToken.None);

        // Assert
        Assert.Single(result1);
        Assert.Single(result2);
        
        // The first call did two TryGet calls (specificKey miss, singleKey miss), then stored it (Set).
        // The second call hit in-memory request cache, so TryGet was NOT called on the persistent cache again.
        // Thus, TryGet count on persistent cache should be exactly 2.
        Assert.Equal(2, mockPersistentCache.TryGetCount);
        Assert.Equal(2, mockPersistentCache.SetCount); // It sets both specific and single keys
    }

    private sealed class DelayingRouteOutcomeProvider : ITravelRouteOutcomeProvider
    {
        private int _activeCalls = 0;
        private readonly object _lock = new();
        public int MaxConcurrentCalls { get; private set; }

        public async Task<IReadOnlyDictionary<string, TravelRouteOutcome>> GetOutcomesAsync(
            TravelRouteOutcomeRequest request, CancellationToken ct)
        {
            int currentActive;
            lock (_lock)
            {
                _activeCalls++;
                currentActive = _activeCalls;
                if (currentActive > MaxConcurrentCalls)
                {
                    MaxConcurrentCalls = currentActive;
                }
            }

            // Simulate slight route lookup/processing latency
            await Task.Delay(50, ct);

            lock (_lock)
            {
                _activeCalls--;
            }

            var results = new Dictionary<string, TravelRouteOutcome>(StringComparer.OrdinalIgnoreCase);
            results["dest-1"] = new TravelRouteOutcome(10, 5.0, "High", TravelRouteResolutionSource.AzureMaps);
            return results;
        }
    }

    private static TravelCoverageRequest BuildTimeDependentRequest(
        DateTimeOffset start,
        DateTimeOffset end,
        int maxTravelTimeMinutes,
        double maxDistanceMiles)
        => new()
        {
            SourcePostcode = "CM1 2FG",
            TimeContext = new TravelCoverageTimeContext
            {
                StartTime = start,
                EndTime = end,
                SearchIntervalMinutes = 30,
                TimingMode = TravelCoverageTimingMode.DepartureTime,
                SlotResponseMode = TravelCoverageSlotResponseMode.Expanded
            },
            Destinations =
            [
                new TravelCoverageDestinationRequest
                {
                    CorrelationId = "dest-1",
                    Postcode = "CM1 2GG",
                    MaxTravelTimeMinutes = maxTravelTimeMinutes,
                    MaxDistanceMiles = maxDistanceMiles
                }
            ],
            RequestContext = new LocationRequestContext { CorrelationId = "req-1" }
        };

    private sealed class StableTimeDependentRouteOutcomeProvider(int etaMinutes, double distanceMiles) : ITravelRouteOutcomeProvider
    {
        public int CallCount { get; private set; }
        public List<DateTimeOffset?> Departures { get; } = [];

        public Task<IReadOnlyDictionary<string, TravelRouteOutcome>> GetOutcomesAsync(
            TravelRouteOutcomeRequest request,
            CancellationToken ct)
        {
            CallCount++;
            Departures.Add(request.TimeContext.RequestedDepartureTime);

            return Task.FromResult<IReadOnlyDictionary<string, TravelRouteOutcome>>(
                new Dictionary<string, TravelRouteOutcome>(StringComparer.OrdinalIgnoreCase)
                {
                    ["dest-1"] = new(etaMinutes, distanceMiles, "High", TravelRouteResolutionSource.AzureMaps)
                });
        }
    }

    private sealed class CountingRouteCache : IRouteCache
    {
        public int TryGetCount { get; private set; }
        public int SetCount { get; private set; }
        private readonly Dictionary<string, RouteResult> _store = new(StringComparer.OrdinalIgnoreCase);

        public bool TryGet(string key, out RouteResult result)
        {
            TryGetCount++;
            return _store.TryGetValue(key, out result!);
        }

        public void Set(string key, RouteResult result, TimeSpan ttl)
        {
            SetCount++;
            _store[key] = result;
        }
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
            DateTimeOffset? departAt = null,
            CancellationToken ct = default)
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
            DateTimeOffset? departAt = null,
            CancellationToken ct = default)
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
            DateTimeOffset? departAt = null,
            CancellationToken ct = default)
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
