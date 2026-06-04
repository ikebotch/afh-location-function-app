namespace AFH.Adviser.Application.Models.OrganisationAssignments;

public enum AdviserScopedOrganisationAssignmentResolutionStatus
{
    Succeeded,
    ValidationFailed,
    AdviserNotFound
}

public sealed record AdviserScopedOrganisationAssignmentResolution(
    AdviserScopedOrganisationAssignmentResolutionStatus Status,
    IReadOnlyList<AdviserScopedOrganisationAssignment> Assignments,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static AdviserScopedOrganisationAssignmentResolution Ok(
        IReadOnlyList<AdviserScopedOrganisationAssignment> assignments)
        => new(AdviserScopedOrganisationAssignmentResolutionStatus.Succeeded, assignments);

    public static AdviserScopedOrganisationAssignmentResolution ValidationFailed(string message)
        => new(
            AdviserScopedOrganisationAssignmentResolutionStatus.ValidationFailed,
            [],
            "INVALID_ASSIGNMENT_SCOPE",
            message);

    public static AdviserScopedOrganisationAssignmentResolution AdviserNotFound(string adviserId)
        => new(
            AdviserScopedOrganisationAssignmentResolutionStatus.AdviserNotFound,
            [],
            "ADVISER_NOT_FOUND",
            $"Adviser '{adviserId}' was not found.");
}
