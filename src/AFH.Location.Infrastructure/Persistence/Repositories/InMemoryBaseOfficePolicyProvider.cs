using AFH.Location.Application.Abstractions.Coverage;
using AFH.Location.Domain;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

public sealed class InMemoryBaseOfficePolicyProvider : IBaseOfficePolicyProvider
{
    private static readonly BaseOfficePolicy _policy = new()
    {
        DefaultOfficeId = "OFF-BROMS",
        RegionOfficeMap =
        {
            ["West Midlands"] = "OFF-BROMS",
            ["North West"]    = "OFF-MANC",
            ["London"]        = "OFF-LOND",
            ["Yorkshire"]     = "OFF-LEEDS",
            ["South West"]    = "OFF-BRIST"
        }
    };

    public Task<BaseOfficePolicy> GetAsync(CancellationToken ct) => Task.FromResult(_policy);
}
