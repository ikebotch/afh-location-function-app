namespace AFH.Location.Service.Core.Contracts.V1.Responses;

public sealed class LocationSearchResponseV1
{
    public string RequestId { get; set; } = default!;
    public DateTime GeneratedAtUtc { get; set; }
    public List<LocationCandidate> Candidates { get; set; } = new();
    public List<ApiWarning> Warnings { get; set; } = new();
}

public sealed class LocationCandidate
{
    public string AdviserId { get; set; } = default!;
    public double AdviserRating { get; set; }
    public bool Preferred { get; set; }
    public string Availability { get; set; } = "Unknown"; // later: enum
    public ProposedSlot ProposedSlotUtc { get; set; } = new();

    public CoverageInfo Coverage { get; set; } = new();
    public TravelToClient TravelToClient { get; set; } = new();
    public TravelToBase TravelToBase { get; set; } = new();
    public TravelToNearestOffice TravelToNearestOffice { get; set; } = new();

    public List<string> Reasons { get; set; } = new();
    public int Rank { get; set; }
    public double? Score { get; set; }
}

public sealed class ProposedSlot
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}

public sealed class CoverageInfo
{
    public bool WithinCoverage { get; set; }
    public string AnchorPostcode { get; set; } = "";
    public double DistanceMiles { get; set; }
}

public sealed class TravelToClient
{
    public int EtaMinutes { get; set; }
    public double DistanceMiles { get; set; }
    public string Confidence { get; set; } = "Low";
}

public sealed class TravelToBase
{
    public int HomeMinutes { get; set; }
    public int OfficeMinutes { get; set; }
}

public sealed class TravelToNearestOffice
{
    public string OfficeId { get; set; } = "";
    public int EtaMinutes { get; set; }
    public double? DistanceMiles { get; set; }
    public string? Confidence { get; set; }
}

public sealed class ApiWarning
{
    public string Code { get; set; } = default!;
    public string Message { get; set; } = default!;
}
