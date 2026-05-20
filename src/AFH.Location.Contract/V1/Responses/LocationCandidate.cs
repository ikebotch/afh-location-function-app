namespace AFH.Location.Contract.V1.Responses;

public sealed class LocationCandidate
{
    public string AdviserId { get; set; } = default!;
    public string MailboxUserId { get; set; } = string.Empty;
    public double AdviserRating { get; set; }
    public bool GoldStar { get; set; }
    public bool Preferred { get; set; }
    public string Availability { get; set; } = "Unknown";
    public ProposedSlot ProposedSlotUtc { get; set; } = new();
    public CoverageInfo Coverage { get; set; } = new();
    public TravelToClient TravelToClient { get; set; } = new();
    public TravelToBase TravelToBase { get; set; } = new();
    public TravelToNearestOffice TravelToNearestOffice { get; set; } = new();
    public BufferInfo Buffers { get; set; } = new();
    public TravelSnapshotResult? TravelSnapshot { get; set; }
    public List<string> Reasons { get; set; } = new();
    public int Rank { get; set; }
    public double? Score { get; set; }
}