using AFH.Adviser.Application.Models.Feed;

namespace AFH.Adviser.Application.Abstractions.Feed;

public interface IAdviserFeedService
{
    Task<AdviserFeedResult> GetCoverageFeedAsync(CancellationToken ct);
}
