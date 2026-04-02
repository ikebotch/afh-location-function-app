namespace AFH.Location.Application.Models.V1;

public sealed class LocationSearchRequest
{
    public string RequestId { get; set; } = default!;
    public LocationMeetingWindow Meeting { get; set; } = new();
    public SearchDestination Destination { get; set; } = new();
    public LocationSearchFilters Filters { get; set; } = new();
}

public sealed class LocationMeetingWindow
{
    public DateTime RequestedStartUtc { get; set; }
    public int DurationMinutes { get; set; }
    public int SearchHorizonMinutes { get; set; }
}

public sealed class SearchDestination
{
    public SearchCoordinates? Coordinates { get; set; }
    public SearchAddress? Address { get; set; }
}

public sealed class SearchCoordinates
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public sealed class SearchAddress
{
    public string Line1 { get; set; } = default!;
    public string? Line2 { get; set; }
    public string Town { get; set; } = default!;
    public string Postcode { get; set; } = default!;
    public string Country { get; set; } = "UK";
}

public sealed class LocationSearchFilters
{
    public string[] Regions { get; set; } = [];
    public string[] AdviserIds { get; set; } = [];
    public string[] RequiredSkills { get; set; } = [];
    public string[] PreferredAdviserIds { get; set; } = [];
    public string[] ExcludeAdviserIds { get; set; } = [];
    public int? MaxCandidates { get; set; }
    public int? BufferMinutes { get; set; }
    public int? CompanyBufferMinutes { get; set; }
    public double? MinAdviserRating { get; set; }
    public double? MaxRankingScore { get; set; }
}

public sealed class LocationSearchResult
{
    public string RequestId { get; set; } = default!;
    public DateTime GeneratedAtUtc { get; set; }
    public List<LocationSearchCandidate> Candidates { get; set; } = [];
    public List<LocationSearchWarning> Warnings { get; set; } = [];
}

public sealed class LocationSearchCandidate
{
    public string AdviserId { get; set; } = default!;
    public double AdviserRating { get; set; }
    public bool GoldStar { get; set; }
    public bool Preferred { get; set; }
    public string Availability { get; set; } = "Unknown";
    public ProposedSlotResult ProposedSlotUtc { get; set; } = new();
    public CoverageResult Coverage { get; set; } = new();
    public TravelToClientResult TravelToClient { get; set; } = new();
    public TravelToBaseResult TravelToBase { get; set; } = new();
    public TravelToNearestOfficeResult TravelToNearestOffice { get; set; } = new();
    public BufferResult Buffers { get; set; } = new();
    public List<string> Reasons { get; set; } = [];
    public int Rank { get; set; }
    public double? Score { get; set; }
}

public sealed class ProposedSlotResult
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}

public sealed class CoverageResult
{
    public bool WithinCoverage { get; set; }
    public string AnchorPostcode { get; set; } = string.Empty;
    public double DistanceMiles { get; set; }
}

public sealed class TravelToClientResult
{
    public int EtaMinutes { get; set; }
    public double DistanceMiles { get; set; }
    public string Confidence { get; set; } = "Low";
}

public sealed class TravelToBaseResult
{
    public int HomeMinutes { get; set; }
    public int OfficeMinutes { get; set; }
}

public sealed class TravelToNearestOfficeResult
{
    public string OfficeId { get; set; } = string.Empty;
    public int EtaMinutes { get; set; }
    public double? DistanceMiles { get; set; }
    public string? Confidence { get; set; }
}

public sealed class BufferResult
{
    public int TravelBufferMinutes { get; set; }
    public int CompanyBufferMinutes { get; set; }
    public int PreMeetingBufferMinutes { get; set; }
    public int PostMeetingBufferMinutes { get; set; }
    public int MaxTravelTimeMinutes { get; set; }
}

public sealed class LocationSearchWarning
{
    public string Code { get; set; } = default!;
    public string Message { get; set; } = default!;
}

public sealed class LocationSearchBatchResult
{
    public DateTime GeneratedAtUtc { get; set; }
    public List<LocationSearchBatchItemResult> Results { get; set; } = [];
}

public sealed class LocationSearchBatchItemResult
{
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public LocationSearchResult? Result { get; set; }
}
