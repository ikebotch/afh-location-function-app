using AFH.Adviser.Infrastructure.Persistence.Auth.Entities;
using AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;

public sealed class AdviserDirectoryDbContext : DbContext
{
    public AdviserDirectoryDbContext(DbContextOptions<AdviserDirectoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<OrganisationAssignmentEntity> OrganisationAssignments => Set<OrganisationAssignmentEntity>();
    public DbSet<DomainRoleEntity> DomainRoles => Set<DomainRoleEntity>();
    public DbSet<DomainPermissionEntity> DomainPermissions => Set<DomainPermissionEntity>();
    public DbSet<DomainUserRoleMappingEntity> DomainUserRoleMappings => Set<DomainUserRoleMappingEntity>();
    public DbSet<DomainUserPermissionMappingEntity> DomainUserPermissionMappings => Set<DomainUserPermissionMappingEntity>();
    public DbSet<DomainRolePermissionEntity> DomainRolePermissions => Set<DomainRolePermissionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganisationAssignmentEntity>(ConfigureOrganisationAssignment);
        modelBuilder.Entity<DomainRoleEntity>(ConfigureDomainRole);
        modelBuilder.Entity<DomainPermissionEntity>(ConfigureDomainPermission);
        modelBuilder.Entity<DomainUserRoleMappingEntity>(ConfigureDomainUserRoleMapping);
        modelBuilder.Entity<DomainUserPermissionMappingEntity>(ConfigureDomainUserPermissionMapping);
        modelBuilder.Entity<DomainRolePermissionEntity>(ConfigureDomainRolePermission);
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

    internal static void ConfigureDomainRole(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DomainRoleEntity> entity)
    {
        entity.ToTable("DomainRoles");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Role).HasMaxLength(100).IsRequired();
        entity.HasIndex(x => x.Role).IsUnique();
    }

    internal static void ConfigureDomainPermission(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DomainPermissionEntity> entity)
    {
        entity.ToTable("DomainPermissions");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Permission).HasMaxLength(128).IsRequired();
        entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(500);
        entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
        entity.HasIndex(x => x.Permission).IsUnique();
        entity.HasIndex(x => new { x.Category, x.IsEnabled });
    }

    internal static void ConfigureDomainUserRoleMapping(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DomainUserRoleMappingEntity> entity)
    {
        entity.ToTable("DomainUserRoleMappings");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Email).HasMaxLength(320);
        entity.Property(x => x.ExternalRole).HasMaxLength(100);
        entity.Property(x => x.ExternalGroupId).HasMaxLength(128);
        entity.HasIndex(x => x.Email);
        entity.HasIndex(x => x.ExternalRole);
        entity.HasIndex(x => x.ExternalGroupId);
        entity.HasIndex(x => new { x.RoleId, x.IsEnabled });
    }

    internal static void ConfigureDomainUserPermissionMapping(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DomainUserPermissionMappingEntity> entity)
    {
        entity.ToTable("DomainUserPermissionMappings");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Email).HasMaxLength(320);
        entity.Property(x => x.ExternalRole).HasMaxLength(100);
        entity.Property(x => x.ExternalGroupId).HasMaxLength(128);
        entity.HasIndex(x => x.Email);
        entity.HasIndex(x => x.ExternalRole);
        entity.HasIndex(x => x.ExternalGroupId);
        entity.HasIndex(x => new { x.PermissionId, x.IsEnabled });
    }

    internal static void ConfigureDomainRolePermission(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DomainRolePermissionEntity> entity)
    {
        entity.ToTable("DomainRolePermissions");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
    }
}
