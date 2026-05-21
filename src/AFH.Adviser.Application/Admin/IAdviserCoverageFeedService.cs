namespace AFH.Adviser.Application.Admin;

public interface IAdviserCoverageFeedService
{
    Task<AdviserCoverageFeedResult> GetCoverageFeedAsync(CancellationToken ct);
}
