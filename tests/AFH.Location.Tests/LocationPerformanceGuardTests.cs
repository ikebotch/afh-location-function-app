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

    private static Adviser NewAdviser(string adviserId) => new()
    {
        AdviserId = adviserId,
        DisplayName = adviserId.ToUpperInvariant(),
        MailboxUserId = adviserId,
        HomePostcode = "AB1 2CD",
        Region = "Region-1",
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
            => throw new NotSupportedException();
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
