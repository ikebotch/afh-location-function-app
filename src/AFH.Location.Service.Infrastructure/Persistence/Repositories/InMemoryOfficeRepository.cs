using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Models;

namespace AFH.Location.Service.Infrastructure.Persistence.Repositories;

public sealed class InMemoryOfficeRepository : IOfficeRepository
{
    private static readonly IReadOnlyList<Office> _offices = new List<Office>
    {
        new() { OfficeId="OFF-BIRM", Name="Birmingham",  Postcode="B3 2BB", Region="West Midlands" },
        new() { OfficeId="OFF-BROMS", Name="Bromsgrove", Postcode="B60 3ET", Region="West Midlands" },
        new() { OfficeId="OFF-MANC", Name="Manchester",  Postcode="M1 1AE", Region="North West" },
        new() { OfficeId="OFF-LOND", Name="London",      Postcode="EC2A 4BX", Region="London" },
        new() { OfficeId="OFF-LEEDS", Name="Leeds",      Postcode="LS1 4AP", Region="Yorkshire" },
        new() { OfficeId="OFF-BRIST", Name="Bristol",    Postcode="BS1 4ST", Region="South West" }
    };

    public Task<IReadOnlyList<Office>> GetAllAsync(CancellationToken ct)
        => Task.FromResult(_offices);
}