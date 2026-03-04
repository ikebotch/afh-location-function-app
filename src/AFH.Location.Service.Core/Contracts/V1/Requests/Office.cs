namespace AFH.Location.Service.Core.Models;

public sealed class Office
{
    public string OfficeId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Postcode { get; init; } = default!;
    public string Region { get; init; } = default!;
}