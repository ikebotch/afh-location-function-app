using AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments.Entities;
using AFH.Adviser.Infrastructure.Persistence.Availability.Entities;
using AFH.Adviser.Infrastructure.Persistence.Coverage.Entities;
using AFH.Adviser.Infrastructure.Persistence.Skills.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;

public sealed class AdviserDirectoryDbContext : DbContext
{
    public AdviserDirectoryDbContext(DbContextOptions<AdviserDirectoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<OrganisationAssignmentEntity> OrganisationAssignments => Set<OrganisationAssignmentEntity>();
    public DbSet<AvailabilityRuleSetEntity> AvailabilityRuleSets => Set<AvailabilityRuleSetEntity>();
    public DbSet<AdviserWorkingPatternRuleEntity> AdviserWorkingPatternRules => Set<AdviserWorkingPatternRuleEntity>();
    public DbSet<AdviserCapacityLimitRuleEntity> AdviserCapacityLimitRules => Set<AdviserCapacityLimitRuleEntity>();
    public DbSet<AdviserSkillCatalogEntity> AdviserSkillCatalog => Set<AdviserSkillCatalogEntity>();
    public DbSet<CoverageRegionEntity> CoverageRegions => Set<CoverageRegionEntity>();
    public DbSet<AdviserRegionAssignmentEntity> AdviserRegionAssignments => Set<AdviserRegionAssignmentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganisationAssignmentEntity>(ConfigureOrganisationAssignment);
        modelBuilder.Entity<AvailabilityRuleSetEntity>(ConfigureAvailabilityRuleSet);
        modelBuilder.Entity<AdviserWorkingPatternRuleEntity>(ConfigureAdviserWorkingPatternRule);
        modelBuilder.Entity<AdviserCapacityLimitRuleEntity>(ConfigureAdviserCapacityLimitRule);
        modelBuilder.Entity<AdviserSkillCatalogEntity>(ConfigureAdviserSkillCatalog);
        modelBuilder.Entity<CoverageRegionEntity>(ConfigureCoverageRegion);
        modelBuilder.Entity<AdviserRegionAssignmentEntity>(ConfigureAdviserRegionAssignment);
    }

    internal static void ConfigureOrganisationAssignment(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<OrganisationAssignmentEntity> entity)
    {
        entity.ToTable("OrganisationAssignments");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Context).HasMaxLength(100).IsRequired();
        entity.Property(x => x.AssignmentType).HasMaxLength(100).IsRequired();
        entity.Property(x => x.OrganisationId).HasMaxLength(100);
        entity.Property(x => x.ClientId).HasMaxLength(100);
        entity.Property(x => x.Region).HasMaxLength(128);
        entity.Property(x => x.AdviserId).HasMaxLength(100);
        entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Email).HasMaxLength(320);
        entity.Property(x => x.MobileNumber).HasMaxLength(50);
        entity.Property(x => x.Channels).HasMaxLength(200).IsRequired();
        entity.HasIndex(x => new { x.Context, x.AssignmentType, x.IsEnabled, x.Priority });
        entity.HasIndex(x => x.Region);
        entity.HasIndex(x => x.AdviserId);
        entity.HasIndex(x => x.ClientId);
        entity.HasIndex(x => x.OrganisationId);
    }

    internal static void ConfigureAvailabilityRuleSet(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<AvailabilityRuleSetEntity> entity)
    {
        entity.ToTable("AdviserAvailabilityRuleSets");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.ProjectContext).HasMaxLength(100).IsRequired();
        entity.Property(x => x.DefaultWorkingDayStart).HasMaxLength(16).IsRequired();
        entity.Property(x => x.DefaultWorkingDayEnd).HasMaxLength(16).IsRequired();
        entity.HasIndex(x => new { x.ProjectContext, x.IsActive, x.UpdatedUtc });
        entity.HasMany(x => x.WorkingPatterns)
            .WithOne(x => x.RuleSet)
            .HasForeignKey(x => x.RuleSetId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(x => x.CapacityLimits)
            .WithOne(x => x.RuleSet)
            .HasForeignKey(x => x.RuleSetId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    internal static void ConfigureAdviserWorkingPatternRule(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<AdviserWorkingPatternRuleEntity> entity)
    {
        entity.ToTable("AdviserWorkingPatternRules");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.AdviserId).HasMaxLength(100).IsRequired();
        entity.Property(x => x.DayOfWeek).HasMaxLength(16);
        entity.Property(x => x.Start).HasMaxLength(16).IsRequired();
        entity.Property(x => x.End).HasMaxLength(16).IsRequired();
        entity.HasIndex(x => new { x.RuleSetId, x.AdviserId, x.IsActive });
    }

    internal static void ConfigureAdviserCapacityLimitRule(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<AdviserCapacityLimitRuleEntity> entity)
    {
        entity.ToTable("AdviserCapacityLimitRules");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.AdviserId).HasMaxLength(100).IsRequired();
        entity.HasIndex(x => new { x.RuleSetId, x.AdviserId, x.IsActive });
    }

    internal static void ConfigureAdviserSkillCatalog(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<AdviserSkillCatalogEntity> entity)
    {
        entity.ToTable("AdviserSkillCatalog");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
        entity.Property(x => x.Category).HasMaxLength(120);
        entity.Property(x => x.Description).HasMaxLength(800);
        entity.Property(x => x.Certification).HasMaxLength(160);
        entity.HasIndex(x => x.Name).IsUnique();
        entity.HasIndex(x => new { x.IsActive, x.Category });
    }

    internal static void ConfigureCoverageRegion(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<CoverageRegionEntity> entity)
    {
        entity.ToTable("CoverageRegions");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
        entity.Property(x => x.LeadAdviserId).HasMaxLength(100);
        entity.Property(x => x.LeadAdviserName).HasMaxLength(200);
        entity.Property(x => x.Postcodes).HasMaxLength(2000).IsRequired();
        entity.Property(x => x.Skills).HasMaxLength(2000).IsRequired();
        entity.HasIndex(x => x.Code).IsUnique();
        entity.HasIndex(x => new { x.IsActive, x.Name });
        entity.HasMany(x => x.AdviserAssignments)
            .WithOne(x => x.Region)
            .HasForeignKey(x => x.RegionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    internal static void ConfigureAdviserRegionAssignment(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<AdviserRegionAssignmentEntity> entity)
    {
        entity.ToTable("AdviserRegionAssignments");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.AdviserId).HasMaxLength(100).IsRequired();
        entity.Property(x => x.AdviserName).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Role).HasMaxLength(100);
        entity.HasIndex(x => new { x.RegionId, x.AdviserId }).IsUnique();
        entity.HasIndex(x => new { x.AdviserId, x.IsActive });
    }
}
