namespace AFH.Location.Application.Models.Travel;

public sealed record LocationRequestContext
{
    public string? CorrelationId { get; init; }
    public string? RequestedBy { get; init; }
}
