namespace AFH.Location.Contract.V1.Requests;

public sealed class Address
{
    public string Line1 { get; set; } = default!;
    public string? Line2 { get; set; }
    public string Town { get; set; } = default!;
    public string Postcode { get; set; } = default!;
    public string Country { get; set; } = "UK";
}
