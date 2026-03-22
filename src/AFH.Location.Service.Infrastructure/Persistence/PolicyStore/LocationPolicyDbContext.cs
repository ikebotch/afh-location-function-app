using AFH.Location.Service.Infrastructure.Persistence.PolicyStore.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Service.Infrastructure.Persistence.PolicyStore;

/// <summary>
/// SQL Server policy store for location-search coverage and availability settings.
/// </summary>
public sealed class LocationPolicyDbContext : DbContext
{
    public LocationPolicyDbContext(DbContextOptions<LocationPolicyDbContext> options)
        : base(options)
    {
    }

    public DbSet<CoverageDefaultPolicyEntity> CoverageDefaults => Set<CoverageDefaultPolicyEntity>();
    public DbSet<CoverageRegionPolicyEntity> CoverageRegions => Set<CoverageRegionPolicyEntity>();
    public DbSet<CoverageAdviserPolicyEntity> CoverageAdvisers => Set<CoverageAdviserPolicyEntity>();
    public DbSet<AvailabilityDefaultPolicyEntity> AvailabilityDefaults => Set<AvailabilityDefaultPolicyEntity>();
    public DbSet<SearchAuditRecordEntity> SearchAuditRecords => Set<SearchAuditRecordEntity>();
    public DbSet<IntegrationOperationAuditEntity> IntegrationOperationAudits => Set<IntegrationOperationAuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CoverageDefaultPolicyEntity>(entity =>
        {
            entity.ToTable("CoverageDefaultPolicies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DefaultRadiusMiles).IsRequired();
            entity.Property(x => x.DefaultMaxTravelTimeMinutes).IsRequired();
        });

        modelBuilder.Entity<CoverageRegionPolicyEntity>(entity =>
        {
            entity.ToTable("CoverageRegionPolicies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Region).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.Region).IsUnique();
        });

        modelBuilder.Entity<CoverageAdviserPolicyEntity>(entity =>
        {
            entity.ToTable("CoverageAdviserPolicies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AdviserId).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.AdviserId).IsUnique();
        });

        modelBuilder.Entity<AvailabilityDefaultPolicyEntity>(entity =>
        {
            entity.ToTable("AvailabilityDefaultPolicies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DefaultTravelBufferMinutes).IsRequired();
            entity.Property(x => x.MaxTravelBufferMinutes).IsRequired();
            entity.Property(x => x.DefaultCompanyBufferMinutes).IsRequired();
            entity.Property(x => x.MaxCompanyBufferMinutes).IsRequired();
            entity.Property(x => x.PreviousClientProximityMinutes).IsRequired();
        });

        modelBuilder.Entity<SearchAuditRecordEntity>(entity =>
        {
            entity.ToTable("SearchAuditRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestId).HasMaxLength(120).IsRequired();
            entity.Property(x => x.CreatedUtc).IsRequired();
            entity.Property(x => x.RequestedStartUtc).IsRequired();
            entity.Property(x => x.DurationMinutes).IsRequired();
            entity.Property(x => x.SearchHorizonMinutes).IsRequired();
            entity.Property(x => x.DestinationPostcode).HasMaxLength(24);
            entity.Property(x => x.RegionsCsv).HasMaxLength(1000);
            entity.Property(x => x.CandidatesReturned).IsRequired();
            entity.Property(x => x.SelectedAdviserId).HasMaxLength(100);
            entity.Property(x => x.SelectedOriginSource).HasMaxLength(40);
            entity.Property(x => x.PayloadJson).IsRequired();

            entity.HasIndex(x => x.CreatedUtc);
            entity.HasIndex(x => x.RequestId);
        });

        modelBuilder.Entity<IntegrationOperationAuditEntity>(entity =>
        {
            entity.ToTable("IntegrationOperationAudit");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ServiceName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.FunctionName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Path).HasMaxLength(512).IsRequired();
            entity.Property(x => x.QueryString).HasMaxLength(2048);
            entity.Property(x => x.CorrelationId).HasMaxLength(128);
            entity.Property(x => x.OperationId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.StatusCode).IsRequired();
            entity.Property(x => x.DurationMs).IsRequired();
            entity.Property(x => x.ErrorType).HasMaxLength(128);
            entity.Property(x => x.ErrorMessage).HasMaxLength(2048);
            entity.Property(x => x.CreatedUtc).IsRequired();

            entity.HasIndex(x => x.CreatedUtc);
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => new { x.FunctionName, x.CreatedUtc });
        });
    }
}
