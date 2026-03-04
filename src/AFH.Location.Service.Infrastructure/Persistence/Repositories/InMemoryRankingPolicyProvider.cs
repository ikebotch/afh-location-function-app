using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Services.Common;
using Microsoft.Extensions.Configuration;

namespace AFH.Location.Service.Infrastructure.Persistence.Repositories;

public sealed class InMemoryRankingPolicyProvider : IRankingPolicyProvider
{
    private readonly IConfiguration _configuration;

    public InMemoryRankingPolicyProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<RankingOptions> GetAsync(CancellationToken ct)
    {
        var policy = new RankingOptions
        {
            AvailablePenalty = _configuration.GetValue<double?>("LocationSearch:Ranking:AvailablePenalty") ?? 0,
            AvailableLaterPenalty = _configuration.GetValue<double?>("LocationSearch:Ranking:AvailableLaterPenalty") ?? 20,
            BusyPenalty = _configuration.GetValue<double?>("LocationSearch:Ranking:BusyPenalty") ?? 100,
            EtaMinuteWeight = _configuration.GetValue<double?>("LocationSearch:Ranking:EtaMinuteWeight") ?? 1.0,
            DistanceMileWeight = _configuration.GetValue<double?>("LocationSearch:Ranking:DistanceMileWeight") ?? 0.5,
            PreferredBonus = _configuration.GetValue<double?>("LocationSearch:Ranking:PreferredBonus") ?? 10,
            LowConfidencePenalty = _configuration.GetValue<double?>("LocationSearch:Ranking:LowConfidencePenalty") ?? 15,
            RegionMatchBonus = _configuration.GetValue<double?>("LocationSearch:Ranking:RegionMatchBonus") ?? 5,
            CoverageFailPenalty = _configuration.GetValue<double?>("LocationSearch:Ranking:CoverageFailPenalty") ?? 250,
            RatingWeight = _configuration.GetValue<double?>("LocationSearch:Ranking:RatingWeight") ?? 5,
            DefaultMinAdviserRating = _configuration.GetValue<double?>("LocationSearch:Ranking:DefaultMinAdviserRating") ?? 0,
            MaxEligibleScore = _configuration.GetValue<double?>("LocationSearch:Ranking:MaxEligibleScore")
        };

        return Task.FromResult(policy);
    }
}
