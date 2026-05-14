namespace AFH.Location.Application.Models.V1.Results;

public sealed class LocationSearchCandidate
{
    public string AdviserId { get; set; } = default!;
    public string MailboxUserId { get; set; } = string.Empty;
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
