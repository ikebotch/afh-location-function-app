using AFH.Adviser.Application.Models.Feed;
using Entities = AFH.Adviser.Domain.Entities;

namespace AFH.Adviser.Application.Abstractions.Feed;

public interface IEffectiveCoveragePolicyResolver
{
    Task<IReadOnlyDictionary<string, EffectiveCoveragePolicy>> ResolveAsync(
        IReadOnlyCollection<Entities.Adviser> advisers,
        CancellationToken ct);
}
