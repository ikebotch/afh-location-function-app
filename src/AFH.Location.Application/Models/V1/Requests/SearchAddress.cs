namespace AFH.Location.Application.Models.V1;

public sealed class SearchAddress
{
    public string Line1 { get; set; } = default!;
    public string? Line2 { get; set; }
    public string Town { get; set; } = default!;
    public string Postcode { get; set; } = default!;
    public string Country { get; set; } = "UK";
}
