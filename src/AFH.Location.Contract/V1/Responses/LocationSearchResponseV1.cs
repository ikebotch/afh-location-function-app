namespace AFH.Location.Contract.V1.Responses;

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
    public string MailboxUserId { get; set; } = string.Empty;
    public double AdviserRating { get; set; }
    public bool GoldStar { get; set; }
    public bool Preferred { get; set; }
    public string Availability { get; set; } = "Unknown"; // later: enum
    public ProposedSlot ProposedSlotUtc { get; set; } = new();

    public CoverageInfo Coverage { get; set; } = new();
    public TravelToClient TravelToClient { get; set; } = new();
    public TravelToBase TravelToBase { get; set; } = new();
    public TravelToNearestOffice TravelToNearestOffice { get; set; } = new();
    public BufferInfo Buffers { get; set; } = new();

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

public sealed class BufferInfo
{
    public int TravelBufferMinutes { get; set; }
    public int CompanyBufferMinutes { get; set; }
    public int PreMeetingBufferMinutes { get; set; }
    public int PostMeetingBufferMinutes { get; set; }
    public int MaxTravelTimeMinutes { get; set; }
}

public sealed class ApiWarning
{
    public string Code { get; set; } = default!;
    public string Message { get; set; } = default!;
}
