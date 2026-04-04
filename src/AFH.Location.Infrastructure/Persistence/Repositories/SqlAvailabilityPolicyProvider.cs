using AFH.Location.Application.Abstractions;
using AFH.Location.Domain;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

/// <summary>
/// Loads availability policy from SQL Server with configuration fallback.
/// </summary>
public sealed class SqlAvailabilityPolicyProvider : IAvailabilityPolicyProvider
{
    private readonly IDbContextFactory<LocationPolicyDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;

    public SqlAvailabilityPolicyProvider(IDbContextFactory<LocationPolicyDbContext> dbContextFactory, IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
    }

    public async Task<AvailabilityPolicy> GetAsync(CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var defaultBuffer = _configuration.GetValue<int?>("LocationSearch:Availability:DefaultBufferMinutes") ?? 0;
        var maxBuffer = _configuration.GetValue<int?>("LocationSearch:Availability:MaxBufferMinutes") ?? 180;
        var defaultCompanyBuffer = _configuration.GetValue<int?>("LocationSearch:Availability:DefaultCompanyBufferMinutes") ?? 30;
        var maxCompanyBuffer = _configuration.GetValue<int?>("LocationSearch:Availability:MaxCompanyBufferMinutes") ?? 180;
        var previousClientProximity = _configuration.GetValue<int?>("LocationSearch:Availability:PreviousClientProximityMinutes") ?? 180;
        var requireCalendarAvailability = _configuration.GetValue<bool?>("LocationSearch:Availability:RequireCalendarAvailability") ?? true;

        var policy = new AvailabilityPolicy
        {
            DefaultBufferMinutes = Math.Max(0, defaultBuffer),
            MaxBufferMinutes = Math.Max(0, maxBuffer),
            DefaultCompanyBufferMinutes = Math.Max(0, defaultCompanyBuffer),
            MaxCompanyBufferMinutes = Math.Max(0, maxCompanyBuffer),
            PreviousClientProximityMinutes = Math.Max(0, previousClientProximity),
            RequireCalendarAvailability = requireCalendarAvailability
        };

        var dbDefault = await db.AvailabilityDefaults
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (dbDefault is null) return policy;

        policy.DefaultBufferMinutes = Math.Max(0, dbDefault.DefaultTravelBufferMinutes);
        policy.MaxBufferMinutes = Math.Max(0, dbDefault.MaxTravelBufferMinutes);
        policy.DefaultCompanyBufferMinutes = Math.Max(0, dbDefault.DefaultCompanyBufferMinutes);
        policy.MaxCompanyBufferMinutes = Math.Max(0, dbDefault.MaxCompanyBufferMinutes);
        policy.PreviousClientProximityMinutes = Math.Max(0, dbDefault.PreviousClientProximityMinutes);
        return policy;
    }
}
