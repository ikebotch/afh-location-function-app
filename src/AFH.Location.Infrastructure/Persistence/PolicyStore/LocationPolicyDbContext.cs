using AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;
using AFH.Common.Errors.EntityFramework.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Infrastructure.Persistence.PolicyStore;

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
    public DbSet<IntegrationOperationAuditEntity> IntegrationOperationAudits => Set<IntegrationOperationAuditEntity>();
    public DbSet<ApplicationLogEntity> ApplicationLogs => Set<ApplicationLogEntity>();
    public DbSet<AdviserReferenceCacheEntity> AdviserReferenceCache => Set<AdviserReferenceCacheEntity>();
    public DbSet<GeoCacheEntryEntity> GeoCacheEntries => Set<GeoCacheEntryEntity>();
    public DbSet<RouteCacheEntryEntity> RouteCacheEntries => Set<RouteCacheEntryEntity>();
    public DbSet<LocationPolicySettingEntity> PolicySettings => Set<LocationPolicySettingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddErrorRecordEntity();

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

        modelBuilder.Entity<ApplicationLogEntity>(entity =>
        {
            entity.ToTable("ApplicationLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Level).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Operation).HasMaxLength(256).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(128);
            entity.Property(x => x.UserId).HasMaxLength(128);
            entity.Property(x => x.ContextId).HasMaxLength(256);
            entity.Property(x => x.EventType).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Result).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.ExceptionType).HasMaxLength(256);
            entity.Property(x => x.ExceptionMessage).HasMaxLength(2048);
            entity.Property(x => x.PayloadJson).HasMaxLength(4096);
            entity.HasIndex(x => x.OccurredUtc);
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => new { x.Category, x.OccurredUtc });
            entity.HasIndex(x => new { x.Operation, x.OccurredUtc });
        });

        modelBuilder.Entity<AdviserReferenceCacheEntity>(entity =>
        {
            entity.ToTable("AdviserReferenceCache");
            entity.HasKey(x => x.AdviserId);
            entity.Property(x => x.AdviserId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.XPlanAdviserId).HasMaxLength(100);
            entity.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.MailboxUserId).HasMaxLength(128);
            entity.Property(x => x.HomePostcode).HasMaxLength(32);
            entity.Property(x => x.Region).HasMaxLength(128);
            entity.Property(x => x.BaseOfficeId).HasMaxLength(100);
            entity.Property(x => x.TeamName).HasMaxLength(128);
            entity.Property(x => x.ManagerId).HasMaxLength(100);
            entity.Property(x => x.SkillsCsv).HasMaxLength(2000);
            entity.HasIndex(x => x.LastSyncedUtc);
        });

        modelBuilder.Entity<GeoCacheEntryEntity>(entity =>
        {
            entity.ToTable("GeoCacheEntries");
            entity.HasKey(x => x.CacheKey);
            entity.Property(x => x.CacheKey).HasMaxLength(512).IsRequired();
            entity.HasIndex(x => x.ExpiresUtc);
        });

        modelBuilder.Entity<RouteCacheEntryEntity>(entity =>
        {
            entity.ToTable("RouteCacheEntries");
            entity.HasKey(x => x.CacheKey);
            entity.Property(x => x.CacheKey).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Confidence).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => x.ExpiresUtc);
        });

        modelBuilder.Entity<LocationPolicySettingEntity>(entity =>
        {
            entity.ToTable("LocationPolicySettings");
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(512).IsRequired();
            entity.Property(x => x.UpdatedUtc).IsRequired();
        });
    }
}
