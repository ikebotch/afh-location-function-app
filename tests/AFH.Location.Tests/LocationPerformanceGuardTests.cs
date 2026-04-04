using AFH.Location.Application.Abstractions;
using AFH.Location.Application.Models.V1;
using AFH.Location.Application.Services.Common;
using AFH.Location.Application.Services.V1;
using AFH.Location.Domain;
using AFH.Location.Domain.Entities;
using AFH.Location.Infrastructure.External.Calendar;
using AFH.Location.Infrastructure.External.Maps;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFH.Location.Tests;

public sealed class LocationPerformanceGuardTests
{
    [Fact]
    public async Task CalendarAvailabilityService_DeduplicatesMailboxIdsBeforeBatchLookup()
    {
        var client = new RecordingCalendarServiceClient();
        var sut = new CalendarAvailabilityService(client);

        await sut.GetAvailabilityAsync(
            ["ADV-1", " adv-1 ", "ADV-2", "", "ADV-2"],
            new LocationMeetingWindow
            {
                RequestedStartUtc = new DateTime(2026, 04, 04, 9, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 60,
                SearchHorizonMinutes = 120
            },
            CancellationToken.None);

        Assert.Equal(["ADV-1", "ADV-2"], client.CapturedIds);
    }

    [Fact]
    public async Task RouteMatrixCoordinator_BatchesOriginsUsingPolicyLimit()
    {
        var matrix = new RecordingRouteMatrixService();
        var policyProvider = new StubRouteMatrixPolicyProvider(new RouteMatrixPolicy
        {
            MaxOriginsPerCall = 2
        });
        var sut = new RouteMatrixCoordinator(matrix, policyProvider);

        var result = await sut.GetRoutesAsync(
            new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
            {
                ["adv-1"] = (51.500001, -0.100001),
                ["adv-2"] = (51.500002, -0.100002),
                ["adv-3"] = (51.500003, -0.100003),
                ["adv-4"] = (51.500004, -0.100004),
                ["adv-5"] = (51.500005, -0.100005)
            },
            (51.600001, -0.200001),
            CancellationToken.None);

        Assert.Equal([2, 2, 1], matrix.BatchSizes);
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task CachedRouteMatrixService_OnlyFetchesMissesFromInnerService()
    {
        var inner = new RecordingRouteMatrixService();
        var cache = new PartialHitRouteCache();
        var sut = new CachedRouteMatrixService(inner, cache);

        var result = await sut.GetAdviserToDestinationAsync(
            new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
            {
                ["adv-1"] = (51.500001, -0.100001),
                ["adv-2"] = (51.500002, -0.100002)
            },
            (51.600001, -0.200001),
            CancellationToken.None);

        Assert.Single(inner.BatchSizes);
        Assert.Equal([1], inner.BatchSizes);
        Assert.Equal(2, result.Count);
        Assert.Equal(12, result["adv-1"].EtaMinutes);
        Assert.Equal(22, result["adv-2"].EtaMinutes);
    }

    [Fact]
    public async Task LocationSearchService_ReusesSingleRouteLookupsAcrossCandidatesWithSharedOrigin()
    {
        var geoPolicyProvider = new StubGeoCachePolicyProvider();
        var geocoding = new StubGeocodingService(new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
        {
            ["AB1 2CD, United Kingdom"] = (51.500001, -0.100001)
        });
        var destinationResolver = new DestinationCoordinateResolver(
            new StubGeoCache(),
            geocoding,
            geoPolicyProvider);
        var adviserResolver = new AdviserCoordinateResolver(
            new StubAdviserGeoCache(),
            geocoding,
            geoPolicyProvider);
        var officeResolver = new OfficeCoordinateResolver(
            new StubOfficeRepository(),
            geocoding,
            geoPolicyProvider,
            new StubGeoCache());

        var searchRequest = new LocationSearchRequest
        {
            RequestId = "req-1",
            Destination = new SearchDestination
            {
                Coordinates = new SearchCoordinates
                {
                    Lat = 51.600001,
                    Lng = -0.200001
                }
            },
            Meeting = new LocationMeetingWindow
            {
                RequestedStartUtc = new DateTime(2026, 04, 06, 10, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 60,
                SearchHorizonMinutes = 120
            },
            Filters = new LocationSearchFilters()
        };

        var candidateSource = new AdviserCandidateSource(new StubAdviserRepository(
        [
            NewAdviser("adv-1"),
            NewAdviser("adv-2")
        ]));

        var routing = new RecordingRoutingService();
        var routingCoordinator = new LocationSearchRoutingCoordinator(
            routing,
            NullLogger<LocationSearchRoutingCoordinator>.Instance);
        var responseCandidateBuilder = new LocationResponseCandidateBuilder(
            new AvailabilityEvaluator(new StubBusinessTimeZoneProvider()),
            routingCoordinator);
        var auditWriter = new LocationSearchAuditWriter(
            new LocationSearchAuditEntryFactory(),
            new StubSearchAuditRepository(),
            NullLogger<LocationSearchAuditWriter>.Instance);
        var sut = new LocationSearchService(
            candidateSource,
            new StubCalendarAvailabilityService(),
            responseCandidateBuilder,
            auditWriter,
            routingCoordinator,
            destinationResolver,
            adviserResolver,
            new StubCoveragePolicyProvider(),
            new RouteMatrixCoordinator(new EmptyRouteMatrixService(), new StubRouteMatrixPolicyProvider(new RouteMatrixPolicy())),
            officeResolver,
            new StubBaseOfficePolicyProvider(),
            new StubAvailabilityPolicyProvider(),
            new StubRankingPolicyProvider(),
            new RankingService(),
            NullLogger<LocationSearchService>.Instance);

        var result = await sut.SearchInPersonAsync(searchRequest, CancellationToken.None);

        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, candidate =>
        {
            Assert.Equal(15, candidate.TravelToClient.EtaMinutes);
            Assert.Equal(18, candidate.TravelToBase.HomeMinutes);
        });
        Assert.Equal(2, routing.CallCount);
        Assert.Equal(
        [
            "51.500001:-0.100001->51.600001:-0.200001",
            "51.600001:-0.200001->51.500001:-0.100001"
        ],
        routing.RouteKeys);
    }

    [Fact]
    public async Task LocationSearchService_ReusesPrecomputedMatrixRoutesForReturnTravel()
    {
        var geoPolicyProvider = new StubGeoCachePolicyProvider();
        var geocoding = new StubGeocodingService(new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
        {
            ["AB1 2CD, United Kingdom"] = (51.500001, -0.100001),
            ["ZX1 1ZZ, United Kingdom"] = (51.700001, -0.300001)
        });
        var destinationResolver = new DestinationCoordinateResolver(
            new StubGeoCache(),
            geocoding,
            geoPolicyProvider);
        var adviserResolver = new AdviserCoordinateResolver(
            new StubAdviserGeoCache(),
            geocoding,
            geoPolicyProvider);
        var officeResolver = new OfficeCoordinateResolver(
            new SingleOfficeRepository(),
            geocoding,
            geoPolicyProvider,
            new StubGeoCache());

        var searchRequest = new LocationSearchRequest
        {
            RequestId = "req-matrix-reuse",
            Destination = new SearchDestination
            {
                Coordinates = new SearchCoordinates
                {
                    Lat = 51.600001,
                    Lng = -0.200001
                }
            },
            Meeting = new LocationMeetingWindow
            {
                RequestedStartUtc = new DateTime(2026, 04, 06, 10, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 60,
                SearchHorizonMinutes = 120
            },
            Filters = new LocationSearchFilters()
        };

        var candidateSource = new AdviserCandidateSource(new StubAdviserRepository([NewAdviser("adv-1")]));
        var routing = new RecordingRoutingService();
        var routingCoordinator = new LocationSearchRoutingCoordinator(
            routing,
            NullLogger<LocationSearchRoutingCoordinator>.Instance);
        var responseCandidateBuilder = new LocationResponseCandidateBuilder(
            new AvailabilityEvaluator(new StubBusinessTimeZoneProvider()),
            routingCoordinator);
        var auditWriter = new LocationSearchAuditWriter(
            new LocationSearchAuditEntryFactory(),
            new StubSearchAuditRepository(),
            NullLogger<LocationSearchAuditWriter>.Instance);
        var routeMatrix = new PrecomputedReturnRouteMatrixService();
        var sut = new LocationSearchService(
            candidateSource,
            new StubCalendarAvailabilityService(),
            responseCandidateBuilder,
            auditWriter,
            routingCoordinator,
            destinationResolver,
            adviserResolver,
            new StubCoveragePolicyProvider(),
            new RouteMatrixCoordinator(routeMatrix, new StubRouteMatrixPolicyProvider(new RouteMatrixPolicy())),
            officeResolver,
            new RegionBaseOfficePolicyProvider(),
            new StubAvailabilityPolicyProvider(),
            new StubRankingPolicyProvider(),
            new RankingService(),
            NullLogger<LocationSearchService>.Instance);

        var result = await sut.SearchInPersonAsync(searchRequest, CancellationToken.None);

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(15, candidate.TravelToClient.EtaMinutes);
        Assert.Equal(18, candidate.TravelToBase.HomeMinutes);
        Assert.Equal(12, candidate.TravelToBase.OfficeMinutes);
        Assert.Equal(12, candidate.TravelToNearestOffice.EtaMinutes);
        Assert.Equal(0, routing.CallCount);
        Assert.Equal(1, routeMatrix.AdviserToDestinationCalls);
        Assert.Equal(2, routeMatrix.OneToManyCalls);
    }

    [Fact]
    public async Task LocationSearchService_DeduplicatesSharedOriginsBeforeClientMatrixRouting()
    {
        var geoPolicyProvider = new StubGeoCachePolicyProvider();
        var geocoding = new StubGeocodingService(new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
        {
            ["AB1 2CD, United Kingdom"] = (51.500001, -0.100001)
        });
        var destinationResolver = new DestinationCoordinateResolver(
            new StubGeoCache(),
            geocoding,
            geoPolicyProvider);
        var adviserResolver = new AdviserCoordinateResolver(
            new StubAdviserGeoCache(),
            geocoding,
            geoPolicyProvider);
        var officeResolver = new OfficeCoordinateResolver(
            new StubOfficeRepository(),
            geocoding,
            geoPolicyProvider,
            new StubGeoCache());

        var searchRequest = new LocationSearchRequest
        {
            RequestId = "req-matrix-client-dedupe",
            Destination = new SearchDestination
            {
                Coordinates = new SearchCoordinates
                {
                    Lat = 51.600001,
                    Lng = -0.200001
                }
            },
            Meeting = new LocationMeetingWindow
            {
                RequestedStartUtc = new DateTime(2026, 04, 06, 10, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 60,
                SearchHorizonMinutes = 120
            },
            Filters = new LocationSearchFilters()
        };

        var candidateSource = new AdviserCandidateSource(new StubAdviserRepository(
        [
            NewAdviser("adv-1"),
            NewAdviser("adv-2")
        ]));

        var routing = new RecordingRoutingService();
        var routingCoordinator = new LocationSearchRoutingCoordinator(
            routing,
            NullLogger<LocationSearchRoutingCoordinator>.Instance);
        var responseCandidateBuilder = new LocationResponseCandidateBuilder(
            new AvailabilityEvaluator(new StubBusinessTimeZoneProvider()),
            routingCoordinator);
        var auditWriter = new LocationSearchAuditWriter(
            new LocationSearchAuditEntryFactory(),
            new StubSearchAuditRepository(),
            NullLogger<LocationSearchAuditWriter>.Instance);
        var routeMatrix = new SharedOriginRouteMatrixService();
        var sut = new LocationSearchService(
            candidateSource,
            new StubCalendarAvailabilityService(),
            responseCandidateBuilder,
            auditWriter,
            routingCoordinator,
            destinationResolver,
            adviserResolver,
            new StubCoveragePolicyProvider(),
            new RouteMatrixCoordinator(routeMatrix, new StubRouteMatrixPolicyProvider(new RouteMatrixPolicy())),
            officeResolver,
            new StubBaseOfficePolicyProvider(),
            new StubAvailabilityPolicyProvider(),
            new StubRankingPolicyProvider(),
            new RankingService(),
            NullLogger<LocationSearchService>.Instance);

        var result = await sut.SearchInPersonAsync(searchRequest, CancellationToken.None);

        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, candidate =>
        {
            Assert.Equal(14, candidate.TravelToClient.EtaMinutes);
            Assert.Equal(18, candidate.TravelToBase.HomeMinutes);
        });
        Assert.Equal(1, routeMatrix.LastAdviserToDestinationOriginCount);
        Assert.Equal(1, routing.CallCount);
        Assert.Equal(
        [
            "51.600001:-0.200001->51.500001:-0.100001"
        ],
        routing.RouteKeys);
    }

    [Fact]
    public async Task LocationSearchService_StartsPolicyLoadsTogetherBeforeContinuing()
    {
        var geoPolicyProvider = new StubGeoCachePolicyProvider();
        var destinationResolver = new DestinationCoordinateResolver(
            new StubGeoCache(),
            new StubGeocodingService(new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)),
            geoPolicyProvider);
        var adviserResolver = new AdviserCoordinateResolver(
            new StubAdviserGeoCache(),
            new StubGeocodingService(new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)),
            geoPolicyProvider);
        var officeResolver = new OfficeCoordinateResolver(
            new StubOfficeRepository(),
            new StubGeocodingService(new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)),
            geoPolicyProvider,
            new StubGeoCache());

        var probe = new PolicyLoadProbe(expectedStarts: 4);
        var sut = new LocationSearchService(
            new AdviserCandidateSource(new StubAdviserRepository([])),
            new StubCalendarAvailabilityService(),
            new LocationResponseCandidateBuilder(
                new AvailabilityEvaluator(new StubBusinessTimeZoneProvider()),
                new LocationSearchRoutingCoordinator(
                    new RecordingRoutingService(),
                    NullLogger<LocationSearchRoutingCoordinator>.Instance)),
            new LocationSearchAuditWriter(
                new LocationSearchAuditEntryFactory(),
                new StubSearchAuditRepository(),
                NullLogger<LocationSearchAuditWriter>.Instance),
            new LocationSearchRoutingCoordinator(
                new RecordingRoutingService(),
                NullLogger<LocationSearchRoutingCoordinator>.Instance),
            destinationResolver,
            adviserResolver,
            new BlockingCoveragePolicyProvider(probe),
            new RouteMatrixCoordinator(new EmptyRouteMatrixService(), new StubRouteMatrixPolicyProvider(new RouteMatrixPolicy())),
            officeResolver,
            new BlockingBaseOfficePolicyProvider(probe),
            new BlockingAvailabilityPolicyProvider(probe),
            new BlockingRankingPolicyProvider(probe),
            new RankingService(),
            NullLogger<LocationSearchService>.Instance);

        var searchTask = sut.SearchInPersonAsync(
            new LocationSearchRequest
            {
                RequestId = "req-policy-loads",
                Destination = new SearchDestination
                {
                    Coordinates = new SearchCoordinates
                    {
                        Lat = 51.600001,
                        Lng = -0.200001
                    }
                },
                Meeting = new LocationMeetingWindow
                {
                    RequestedStartUtc = new DateTime(2026, 04, 06, 10, 0, 0, DateTimeKind.Utc),
                    DurationMinutes = 60,
                    SearchHorizonMinutes = 120
                },
                Filters = new LocationSearchFilters()
            },
            CancellationToken.None);

        await probe.WhenAllStarted;
        Assert.Equal(4, probe.StartCount);

        probe.Release();

        var result = await searchTask;
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task LocationSearchService_ReturnEnrichmentPrecomputeScalesWithUniqueHomesAndOffices()
    {
        var geoPolicyProvider = new StubGeoCachePolicyProvider();
        var geocoding = new StubGeocodingService(new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
        {
            ["AB1 2CD, United Kingdom"] = (51.500001, -0.100001),
            ["CD3 4EF, United Kingdom"] = (51.700001, -0.300001),
            ["ZX1 1ZZ, United Kingdom"] = (51.650001, -0.250001),
            ["ZX2 2ZZ, United Kingdom"] = (51.800001, -0.350001)
        });
        var destinationResolver = new DestinationCoordinateResolver(
            new StubGeoCache(),
            geocoding,
            geoPolicyProvider);
        var adviserResolver = new AdviserCoordinateResolver(
            new StubAdviserGeoCache(),
            geocoding,
            geoPolicyProvider);
        var officeResolver = new OfficeCoordinateResolver(
            new MultiOfficeRepository(),
            geocoding,
            geoPolicyProvider,
            new StubGeoCache());

        var candidateSource = new AdviserCandidateSource(new StubAdviserRepository(
        [
            NewAdviser("adv-1", "AB1 2CD", "Region-1"),
            NewAdviser("adv-2", "AB1 2CD", "Region-1"),
            NewAdviser("adv-3", "CD3 4EF", "Region-2"),
            NewAdviser("adv-4", "CD3 4EF", "Region-2")
        ]));

        var routing = new RecordingRoutingService();
        var routingCoordinator = new LocationSearchRoutingCoordinator(
            routing,
            NullLogger<LocationSearchRoutingCoordinator>.Instance);
        var responseCandidateBuilder = new LocationResponseCandidateBuilder(
            new AvailabilityEvaluator(new StubBusinessTimeZoneProvider()),
            routingCoordinator);
        var auditWriter = new LocationSearchAuditWriter(
            new LocationSearchAuditEntryFactory(),
            new StubSearchAuditRepository(),
            NullLogger<LocationSearchAuditWriter>.Instance);
        var routeMatrix = new EnrichmentMeasurementRouteMatrixService();
        var sut = new LocationSearchService(
            candidateSource,
            new StubCalendarAvailabilityService(),
            responseCandidateBuilder,
            auditWriter,
            routingCoordinator,
            destinationResolver,
            adviserResolver,
            new StubCoveragePolicyProvider(),
            new RouteMatrixCoordinator(routeMatrix, new StubRouteMatrixPolicyProvider(new RouteMatrixPolicy())),
            officeResolver,
            new MultiRegionBaseOfficePolicyProvider(),
            new StubAvailabilityPolicyProvider(),
            new StubRankingPolicyProvider(),
            new RankingService(),
            NullLogger<LocationSearchService>.Instance);

        var result = await sut.SearchInPersonAsync(NewSearchRequest("req-enrichment-precompute"), CancellationToken.None);

        Assert.Equal(4, result.Candidates.Count);
        Assert.Equal(0, routing.CallCount);
        Assert.Equal(1, routeMatrix.AdviserToDestinationCalls);
        Assert.Equal(2, routeMatrix.OneToManyCalls);
        Assert.Equal([2, 2], routeMatrix.OneToManyDestinationCounts);
    }

    [Fact]
    public async Task LocationSearchService_ReturnEnrichmentFallbackScalesWithUniqueHomesAndOffices()
    {
        var geoPolicyProvider = new StubGeoCachePolicyProvider();
        var geocoding = new StubGeocodingService(new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
        {
            ["AB1 2CD, United Kingdom"] = (51.500001, -0.100001),
            ["CD3 4EF, United Kingdom"] = (51.700001, -0.300001),
            ["ZX1 1ZZ, United Kingdom"] = (51.650001, -0.250001),
            ["ZX2 2ZZ, United Kingdom"] = (51.800001, -0.350001)
        });
        var destinationResolver = new DestinationCoordinateResolver(
            new StubGeoCache(),
            geocoding,
            geoPolicyProvider);
        var adviserResolver = new AdviserCoordinateResolver(
            new StubAdviserGeoCache(),
            geocoding,
            geoPolicyProvider);
        var officeResolver = new OfficeCoordinateResolver(
            new MultiOfficeRepository(),
            geocoding,
            geoPolicyProvider,
            new StubGeoCache());

        var candidateSource = new AdviserCandidateSource(new StubAdviserRepository(
        [
            NewAdviser("adv-1", "AB1 2CD", "Region-1"),
            NewAdviser("adv-2", "AB1 2CD", "Region-1"),
            NewAdviser("adv-3", "CD3 4EF", "Region-2"),
            NewAdviser("adv-4", "CD3 4EF", "Region-2")
        ]));

        var routing = new RecordingRoutingService();
        var routingCoordinator = new LocationSearchRoutingCoordinator(
            routing,
            NullLogger<LocationSearchRoutingCoordinator>.Instance);
        var responseCandidateBuilder = new LocationResponseCandidateBuilder(
            new AvailabilityEvaluator(new StubBusinessTimeZoneProvider()),
            routingCoordinator);
        var auditWriter = new LocationSearchAuditWriter(
            new LocationSearchAuditEntryFactory(),
            new StubSearchAuditRepository(),
            NullLogger<LocationSearchAuditWriter>.Instance);
        var sut = new LocationSearchService(
            candidateSource,
            new StubCalendarAvailabilityService(),
            responseCandidateBuilder,
            auditWriter,
            routingCoordinator,
            destinationResolver,
            adviserResolver,
            new StubCoveragePolicyProvider(),
            new RouteMatrixCoordinator(new ClientOnlyRouteMatrixService(), new StubRouteMatrixPolicyProvider(new RouteMatrixPolicy())),
            officeResolver,
            new MultiRegionBaseOfficePolicyProvider(),
            new StubAvailabilityPolicyProvider(),
            new StubRankingPolicyProvider(),
            new RankingService(),
            NullLogger<LocationSearchService>.Instance);

        var result = await sut.SearchInPersonAsync(NewSearchRequest("req-enrichment-fallback"), CancellationToken.None);

        Assert.Equal(4, result.Candidates.Count);
        Assert.Equal(4, routing.CallCount);
        Assert.Equal(
        [
            "51.600001:-0.200001->51.500001:-0.100001",
            "51.600001:-0.200001->51.650001:-0.250001",
            "51.600001:-0.200001->51.700001:-0.300001",
            "51.600001:-0.200001->51.800001:-0.350001"
        ],
        routing.RouteKeys.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static LocationSearchRequest NewSearchRequest(string requestId) => new()
    {
        RequestId = requestId,
        Destination = new SearchDestination
        {
            Coordinates = new SearchCoordinates
            {
                Lat = 51.600001,
                Lng = -0.200001
            }
        },
        Meeting = new LocationMeetingWindow
        {
            RequestedStartUtc = new DateTime(2026, 04, 06, 10, 0, 0, DateTimeKind.Utc),
            DurationMinutes = 60,
            SearchHorizonMinutes = 120
        },
        Filters = new LocationSearchFilters()
    };

    private static Adviser NewAdviser(string adviserId) => NewAdviser(adviserId, "AB1 2CD", "Region-1");

    private static Adviser NewAdviser(string adviserId, string homePostcode, string region) => new()
    {
        AdviserId = adviserId,
        DisplayName = adviserId.ToUpperInvariant(),
        MailboxUserId = adviserId,
        HomePostcode = homePostcode,
        Region = region,
        Rating = 4.5,
        IsActive = true,
        IsBookable = true
    };

    private sealed class RecordingCalendarServiceClient : ICalendarServiceClient
    {
        public IReadOnlyList<string> CapturedIds { get; private set; } = Array.Empty<string>();

        public Task<AdviserAvailability> GetAdviserAvailabilityAsync(
            string adviserId,
            LocationMeetingWindow window,
            CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<AdviserAvailability>> GetAdviserAvailabilityBatchAsync(
            IReadOnlyList<string> adviserIds,
            LocationMeetingWindow window,
            CancellationToken ct)
        {
            CapturedIds = adviserIds.ToArray();
            return Task.FromResult<IReadOnlyList<AdviserAvailability>>(
            [
                new AdviserAvailability { AdviserId = "ADV-1" },
                new AdviserAvailability { AdviserId = "ADV-2" }
            ]);
        }

        public Task<CalendarSubscriptionEnsureSummary> EnsureSubscriptionsAsync(
            IReadOnlyList<string> userIds,
            CancellationToken ct)
            => throw new NotSupportedException();

        public Task<CalendarAppointmentResult> CreateAppointmentAsync(
            CreateCalendarAppointmentRequest request,
            CancellationToken ct)
            => throw new NotSupportedException();

        public Task UpdateAppointmentAsync(
            UpdateCalendarAppointmentRequest request,
            CancellationToken ct)
            => throw new NotSupportedException();

        public Task CancelAppointmentAsync(
            CancelCalendarAppointmentRequest request,
            CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class RecordingRouteMatrixService : IRouteMatrixService
    {
        public List<int> BatchSizes { get; } = [];

        public Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
            IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
            (double Lat, double Lng) destination,
            CancellationToken ct)
        {
            BatchSizes.Add(adviserOrigins.Count);

            IReadOnlyDictionary<string, RouteResult> routes = adviserOrigins.ToDictionary(
                kvp => kvp.Key,
                kvp => new RouteResult(kvp.Key.Equals("adv-2", StringComparison.OrdinalIgnoreCase) ? 22 : 11, 5.5, "High"),
                StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(routes);
        }

        public Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
            (double Lat, double Lng) origin,
            IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
            CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class StubRouteMatrixPolicyProvider : IRouteMatrixPolicyProvider
    {
        private readonly RouteMatrixPolicy _policy;

        public StubRouteMatrixPolicyProvider(RouteMatrixPolicy policy)
        {
            _policy = policy;
        }

        public Task<RouteMatrixPolicy> GetAsync(CancellationToken ct) => Task.FromResult(_policy);
    }

    private sealed class PartialHitRouteCache : IRouteCache
    {
        public bool TryGet(string key, out RouteResult result)
        {
            if (key.StartsWith("adv-1:", StringComparison.OrdinalIgnoreCase))
            {
                result = new RouteResult(12, 4.2, "High");
                return true;
            }

            result = default!;
            return false;
        }

        public void Set(string key, RouteResult result, TimeSpan ttl)
        {
        }
    }

    private sealed class RecordingRoutingService : IRoutingService
    {
        public int CallCount => RouteKeys.Count;
        public List<string> RouteKeys { get; } = [];

        public Task<RouteResult> GetRouteAsync(
            (double Lat, double Lng) origin,
            (double Lat, double Lng) destination,
            CancellationToken ct)
        {
            RouteKeys.Add($"{origin.Lat:F6}:{origin.Lng:F6}->{destination.Lat:F6}:{destination.Lng:F6}");

            var etaMinutes = origin.Lat < destination.Lat ? 15 : 18;
            return Task.FromResult(new RouteResult(etaMinutes, 7.5, "High"));
        }
    }

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

    private sealed class PrecomputedReturnRouteMatrixService : IRouteMatrixService
    {
        public int AdviserToDestinationCalls { get; private set; }
        public int OneToManyCalls { get; private set; }

        public Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
            IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
            (double Lat, double Lng) destination,
            CancellationToken ct)
        {
            AdviserToDestinationCalls++;
            IReadOnlyDictionary<string, RouteResult> result = adviserOrigins.ToDictionary(
                x => x.Key,
                _ => new RouteResult(15, 7.5, "High"),
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }

        public Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
            (double Lat, double Lng) origin,
            IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
            CancellationToken ct)
        {
            OneToManyCalls++;
            IReadOnlyDictionary<string, RouteResult> result = destinations.ToDictionary(
                x => x.Key,
                x => x.Key.StartsWith("51.500001:", StringComparison.OrdinalIgnoreCase)
                    ? new RouteResult(18, 7.5, "High")
                    : new RouteResult(12, 9.5, "High"),
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }
    }

    private sealed class SharedOriginRouteMatrixService : IRouteMatrixService
    {
        public int LastAdviserToDestinationOriginCount { get; private set; }

        public Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
            IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
            (double Lat, double Lng) destination,
            CancellationToken ct)
        {
            LastAdviserToDestinationOriginCount = adviserOrigins.Count;
            IReadOnlyDictionary<string, RouteResult> result = adviserOrigins.ToDictionary(
                x => x.Key,
                _ => new RouteResult(14, 6.5, "High"),
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }

        public Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
            (double Lat, double Lng) origin,
            IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
            CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, RouteResult>>(
                new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed class EnrichmentMeasurementRouteMatrixService : IRouteMatrixService
    {
        public int AdviserToDestinationCalls { get; private set; }
        public int OneToManyCalls { get; private set; }
        public List<int> OneToManyDestinationCounts { get; } = [];

        public Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
            IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
            (double Lat, double Lng) destination,
            CancellationToken ct)
        {
            AdviserToDestinationCalls++;
            IReadOnlyDictionary<string, RouteResult> result = adviserOrigins.ToDictionary(
                x => x.Key,
                _ => new RouteResult(15, 7.5, "High"),
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }

        public Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
            (double Lat, double Lng) origin,
            IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
            CancellationToken ct)
        {
            OneToManyCalls++;
            OneToManyDestinationCounts.Add(destinations.Count);
            IReadOnlyDictionary<string, RouteResult> result = destinations.ToDictionary(
                x => x.Key,
                x => x.Key.Contains("51.500001:", StringComparison.OrdinalIgnoreCase)
                    ? new RouteResult(18, 7.5, "High")
                    : x.Key.Contains("51.700001:", StringComparison.OrdinalIgnoreCase)
                        ? new RouteResult(21, 9.0, "High")
                        : x.Key.Equals("OFF-1", StringComparison.OrdinalIgnoreCase)
                            ? new RouteResult(12, 9.5, "High")
                            : new RouteResult(16, 12.0, "High"),
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }
    }

    private sealed class ClientOnlyRouteMatrixService : IRouteMatrixService
    {
        public Task<IReadOnlyDictionary<string, RouteResult>> GetAdviserToDestinationAsync(
            IReadOnlyDictionary<string, (double Lat, double Lng)> adviserOrigins,
            (double Lat, double Lng) destination,
            CancellationToken ct)
        {
            IReadOnlyDictionary<string, RouteResult> result = adviserOrigins.ToDictionary(
                x => x.Key,
                _ => new RouteResult(15, 7.5, "High"),
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }

        public Task<IReadOnlyDictionary<string, RouteResult>> GetOneToManyAsync(
            (double Lat, double Lng) origin,
            IReadOnlyDictionary<string, (double Lat, double Lng)> destinations,
            CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, RouteResult>>(
                new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed class PolicyLoadProbe
    {
        private readonly int _expectedStarts;
        private readonly TaskCompletionSource _allStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _startCount;

        public PolicyLoadProbe(int expectedStarts)
        {
            _expectedStarts = expectedStarts;
        }

        public int StartCount => _startCount;
        public Task WhenAllStarted => _allStarted.Task;

        public async Task WaitForReleaseAsync()
        {
            if (Interlocked.Increment(ref _startCount) == _expectedStarts)
                _allStarted.TrySetResult();

            await _release.Task;
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class BlockingCoveragePolicyProvider : ICoveragePolicyProvider
    {
        private readonly PolicyLoadProbe _probe;

        public BlockingCoveragePolicyProvider(PolicyLoadProbe probe)
        {
            _probe = probe;
        }

        public async Task<CoveragePolicy> GetAsync(CancellationToken ct)
        {
            await _probe.WaitForReleaseAsync();
            return new CoveragePolicy
            {
                DefaultRadiusMiles = 100,
                DefaultMaxTravelTimeMinutes = 120
            };
        }
    }

    private sealed class BlockingBaseOfficePolicyProvider : IBaseOfficePolicyProvider
    {
        private readonly PolicyLoadProbe _probe;

        public BlockingBaseOfficePolicyProvider(PolicyLoadProbe probe)
        {
            _probe = probe;
        }

        public async Task<BaseOfficePolicy> GetAsync(CancellationToken ct)
        {
            await _probe.WaitForReleaseAsync();
            return new BaseOfficePolicy();
        }
    }

    private sealed class BlockingAvailabilityPolicyProvider : IAvailabilityPolicyProvider
    {
        private readonly PolicyLoadProbe _probe;

        public BlockingAvailabilityPolicyProvider(PolicyLoadProbe probe)
        {
            _probe = probe;
        }

        public async Task<AvailabilityPolicy> GetAsync(CancellationToken ct)
        {
            await _probe.WaitForReleaseAsync();
            return new AvailabilityPolicy
            {
                RequireCalendarAvailability = false
            };
        }
    }

    private sealed class BlockingRankingPolicyProvider : IRankingPolicyProvider
    {
        private readonly PolicyLoadProbe _probe;

        public BlockingRankingPolicyProvider(PolicyLoadProbe probe)
        {
            _probe = probe;
        }

        public async Task<RankingOptions> GetAsync(CancellationToken ct)
        {
            await _probe.WaitForReleaseAsync();
            return new RankingOptions();
        }
    }

    private sealed class StubAdviserRepository : IAdviserRepository
    {
        private readonly IReadOnlyList<Adviser> _advisers;

        public StubAdviserRepository(IReadOnlyList<Adviser> advisers)
        {
            _advisers = advisers;
        }

        public Task<IReadOnlyList<Adviser>> GetAllAsync(IReadOnlyCollection<string>? adviserIds, CancellationToken ct)
            => Task.FromResult(_advisers);
    }

    private sealed class StubCalendarAvailabilityService : ICalendarAvailabilityService
    {
        public Task<IReadOnlyList<AdviserAvailability>> GetAvailabilityAsync(
            IReadOnlyList<string> adviserIds,
            LocationMeetingWindow window,
            CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AdviserAvailability>>([]);
    }

    private sealed class StubCoveragePolicyProvider : ICoveragePolicyProvider
    {
        public Task<CoveragePolicy> GetAsync(CancellationToken ct)
            => Task.FromResult(new CoveragePolicy
            {
                DefaultRadiusMiles = 100,
                DefaultMaxTravelTimeMinutes = 120
            });
    }

    private sealed class StubBaseOfficePolicyProvider : IBaseOfficePolicyProvider
    {
        public Task<BaseOfficePolicy> GetAsync(CancellationToken ct)
            => Task.FromResult(new BaseOfficePolicy());
    }

    private sealed class RegionBaseOfficePolicyProvider : IBaseOfficePolicyProvider
    {
        public Task<BaseOfficePolicy> GetAsync(CancellationToken ct)
            => Task.FromResult(new BaseOfficePolicy
            {
                DefaultOfficeId = "OFF-1",
                RegionOfficeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Region-1"] = "OFF-1"
                }
            });
    }

    private sealed class MultiRegionBaseOfficePolicyProvider : IBaseOfficePolicyProvider
    {
        public Task<BaseOfficePolicy> GetAsync(CancellationToken ct)
            => Task.FromResult(new BaseOfficePolicy
            {
                DefaultOfficeId = "OFF-1",
                RegionOfficeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Region-1"] = "OFF-1",
                    ["Region-2"] = "OFF-2"
                }
            });
    }

    private sealed class StubAvailabilityPolicyProvider : IAvailabilityPolicyProvider
    {
        public Task<AvailabilityPolicy> GetAsync(CancellationToken ct)
            => Task.FromResult(new AvailabilityPolicy
            {
                RequireCalendarAvailability = false
            });
    }

    private sealed class StubRankingPolicyProvider : IRankingPolicyProvider
    {
        public Task<RankingOptions> GetAsync(CancellationToken ct)
            => Task.FromResult(new RankingOptions());
    }

    private sealed class StubSearchAuditRepository : ISearchAuditRepository
    {
        public Task SaveAsync(SearchAuditEntry entry, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StubBusinessTimeZoneProvider : IBusinessTimeZoneProvider
    {
        public string TimeZoneId => "UTC";
    }

    private sealed class StubOfficeRepository : IOfficeRepository
    {
        public Task<IReadOnlyList<Office>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Office>>([]);
    }

    private sealed class SingleOfficeRepository : IOfficeRepository
    {
        public Task<IReadOnlyList<Office>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Office>>(
            [
                new Office
                {
                    OfficeId = "OFF-1",
                    Name = "Office 1",
                    Postcode = "ZX1 1ZZ",
                    Region = "Region-1"
                }
            ]);
    }

    private sealed class MultiOfficeRepository : IOfficeRepository
    {
        public Task<IReadOnlyList<Office>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Office>>(
            [
                new Office
                {
                    OfficeId = "OFF-1",
                    Name = "Office 1",
                    Postcode = "ZX1 1ZZ",
                    Region = "Region-1"
                },
                new Office
                {
                    OfficeId = "OFF-2",
                    Name = "Office 2",
                    Postcode = "ZX2 2ZZ",
                    Region = "Region-2"
                }
            ]);
    }

    private sealed class StubGeoCachePolicyProvider : IGeoCachePolicyProvider
    {
        private static readonly GeoCachePolicy Policy = new()
        {
            AdviserHomeTtl = TimeSpan.FromMinutes(30),
            AdviserOfficeTtl = TimeSpan.FromMinutes(30),
            DestinationTtl = TimeSpan.FromMinutes(30),
            FailureTtl = TimeSpan.FromMinutes(5),
            SuccessTtl = TimeSpan.FromMinutes(30)
        };

        public Task<GeoCachePolicy> GetAsync(CancellationToken ct) => Task.FromResult(Policy);
    }

    private sealed class StubGeoCache : IGeoCache
    {
        public bool TryGet(string key, out (double Lat, double Lng) coords)
        {
            coords = default;
            return false;
        }

        public void Set(string key, (double Lat, double Lng) coords, TimeSpan ttl)
        {
        }
    }

    private sealed class StubAdviserGeoCache : IAdviserGeoCache
    {
        public bool TryGet(string key, out (double Lat, double Lng) coords)
        {
            coords = default;
            return false;
        }

        public void Set(string key, (double Lat, double Lng) coords, TimeSpan ttl)
        {
        }
    }

    private sealed class StubGeocodingService : IGeocodingService
    {
        private readonly IReadOnlyDictionary<string, (double Lat, double Lng)> _results;

        public StubGeocodingService(IReadOnlyDictionary<string, (double Lat, double Lng)> results)
        {
            _results = results;
        }

        public Task<(double Lat, double Lng)> GeocodeAsync(string address, CancellationToken ct)
            => Task.FromResult(_results.TryGetValue(address, out var coords) ? coords : (0d, 0d));
    }
}
