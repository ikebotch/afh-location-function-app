namespace AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments.Entities;

public sealed class OrganisationAssignmentEntity
{
    public Guid Id { get; set; }
    public string Context { get; set; } = default!;
    public string AssignmentType { get; set; } = default!;
    public string? OrganisationId { get; set; }
    public string? ClientId { get; set; }
    public string? Region { get; set; }
    public string? AdviserId { get; set; }
    public string DisplayName { get; set; } = default!;
    public string? Email { get; set; }
    public string? MobileNumber { get; set; }
    public string Channels { get; set; } = "Email";
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}

