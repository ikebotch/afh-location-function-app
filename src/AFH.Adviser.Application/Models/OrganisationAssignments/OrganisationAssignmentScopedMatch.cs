namespace AFH.Adviser.Application.Models.OrganisationAssignments;

public sealed record OrganisationAssignmentScopedMatch(
    OrganisationAssignment Assignment,
    string MatchLevel,
    string? MatchedOrganisationId,
    string? MatchedRegion,
    string? MatchedAdviserId,
    int Rank);
