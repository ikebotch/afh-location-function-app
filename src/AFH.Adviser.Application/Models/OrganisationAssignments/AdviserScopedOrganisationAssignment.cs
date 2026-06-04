namespace AFH.Adviser.Application.Models.OrganisationAssignments;

public sealed record AdviserScopedOrganisationAssignment(
    Guid Id,
    string Context,
    string AssignmentType,
    string? OrganisationId,
    string? ClientId,
    string? Region,
    string? AdviserId,
    string DisplayName,
    string? Email,
    string? MobileNumber,
    IReadOnlyList<string> Channels,
    bool IsEnabled,
    int Priority,
    string MatchLevel,
    string? MatchedOrganisationId,
    string? MatchedRegion,
    string? MatchedAdviserId);

public static class OrganisationAssignmentMatchLevels
{
    public const string Adviser = "Adviser";
    public const string OrganisationRegion = "OrganisationRegion";
    public const string Organisation = "Organisation";
    public const string Region = "Region";
    public const string Fallback = "Fallback";
}
