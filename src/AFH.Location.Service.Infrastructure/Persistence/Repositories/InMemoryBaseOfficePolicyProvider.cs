using AFH.Location.Service.Application.Abstractions;
using AFH.Location.Service.Domain;

namespace AFH.Location.Service.Infrastructure.Persistence.Repositories;

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
