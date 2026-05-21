namespace AFH.Adviser.Application.Admin;

public interface IAdviserCoverageFeedService
{
    Task<AdviserCoverageFeedResult> GetCoverageFeedAsync(DateTime? sinceUtc, CancellationToken ct);
}
