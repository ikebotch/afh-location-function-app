namespace AFH.Location.Service.Core.Domain.Entities;

public sealed class Adviser
{
    public string AdviserId { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string HomePostcode { get; set; } = default!;
    public string Region { get; set; } = default!;
    public IReadOnlyCollection<string> Skills { get; set; } = Array.Empty<string>();
    public double Rating { get; set; }
    public bool IsActive { get; set; }
}
