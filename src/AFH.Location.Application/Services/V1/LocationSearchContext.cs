using AFH.Location.Application.Abstractions;
using AFH.Location.Application.Models.V1;
using AFH.Location.Application.Services.Common;
using AFH.Location.Domain;
using System.Collections.Concurrent;

namespace AFH.Location.Application.Services.V1;

internal sealed class LocationSearchContext
{
    public required LocationSearchRequest Request { get; init; }
    public required LocationSearchResult Response { get; init; }

    public required (double Lat, double Lng) Destination { get; set; }
    public required DestinationResolved DestResolved { get; set; }

    public IReadOnlyList<AdviserCandidate> Candidates { get; set; } = Array.Empty<AdviserCandidate>();

    public Dictionary<string, (double Lat, double Lng)> AdviserOrigins { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> OriginSourceById { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, double> AirMilesById { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, RouteResult> RoutesToClient { get; set; } =
        new Dictionary<string, RouteResult>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, AdviserAvailability> AvailabilityById { get; set; } =
        new Dictionary<string, AdviserAvailability>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, (double Lat, double Lng)> OfficeCoords { get; set; } =
        new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase);

    public BaseOfficePolicy BaseOfficePolicy { get; set; } = new();
    public CoveragePolicy CoveragePolicy { get; set; } = new();
    public RankingOptions RankingPolicy { get; set; } = new();
    public AvailabilityPolicy AvailabilityPolicy { get; set; } = new();

    public string? NearestOfficeId { get; set; }
    public RouteResult? NearestOfficeRoute { get; set; }

    public ConcurrentDictionary<string, int> OfficeRouteMinutesByOfficeId { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentDictionary<string, Lazy<Task<RouteResult>>> RouteLookupsByPath { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}
