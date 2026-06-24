using AFH.Identity.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<DomainUserProfileEntity> DomainUserProfiles => Set<DomainUserProfileEntity>();
    public DbSet<DomainRoleEntity> DomainRoles => Set<DomainRoleEntity>();
    public DbSet<DomainPermissionEntity> DomainPermissions => Set<DomainPermissionEntity>();
    public DbSet<DomainRolePermissionEntity> DomainRolePermissions => Set<DomainRolePermissionEntity>();
    public DbSet<DomainUserRoleMappingEntity> DomainUserRoleMappings => Set<DomainUserRoleMappingEntity>();
    public DbSet<DomainUserPermissionMappingEntity> DomainUserPermissionMappings => Set<DomainUserPermissionMappingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DomainUserProfileEntity>(ConfigureDomainUserProfile);
        modelBuilder.Entity<DomainRoleEntity>(ConfigureDomainRole);
        modelBuilder.Entity<DomainPermissionEntity>(ConfigureDomainPermission);
        modelBuilder.Entity<DomainRolePermissionEntity>(ConfigureDomainRolePermission);
        modelBuilder.Entity<DomainUserRoleMappingEntity>(ConfigureDomainUserRoleMapping);
        modelBuilder.Entity<DomainUserPermissionMappingEntity>(ConfigureDomainUserPermissionMapping);
    }

    private static void ConfigureDomainUserProfile(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DomainUserProfileEntity> entity)
    {
        entity.ToTable("DomainUserProfiles");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.ExternalSubject).HasMaxLength(160).IsRequired();
        entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
        entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        entity.Property(x => x.AdviserId).HasMaxLength(100);
        entity.Property(x => x.JobRole).HasMaxLength(100);
        entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
        entity.HasIndex(x => x.ExternalSubject).IsUnique();
        entity.HasIndex(x => x.Email);
        entity.HasIndex(x => x.AdviserId);
    }

    private static void ConfigureDomainRole(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DomainRoleEntity> entity)
    {
        entity.ToTable("DomainRoles");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Role).HasMaxLength(100).IsRequired();
        entity.HasIndex(x => x.Role).IsUnique();
    }

    private static void ConfigureDomainPermission(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DomainPermissionEntity> entity)
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

    private static void ConfigureDomainRolePermission(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DomainRolePermissionEntity> entity)
    {
        entity.ToTable("DomainRolePermissions");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.PermissionId).IsRequired();
        entity.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
    }

    private static void ConfigureDomainUserRoleMapping(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DomainUserRoleMappingEntity> entity)
    {
        entity.ToTable("DomainUserRoleMappings");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.UserProfileId);
        entity.Property(x => x.Email).HasMaxLength(320);
        entity.Property(x => x.ExternalRole).HasMaxLength(100);
        entity.Property(x => x.ExternalGroupId).HasMaxLength(128);
        entity.HasIndex(x => x.UserProfileId);
        entity.HasIndex(x => x.Email);
        entity.HasIndex(x => x.ExternalRole);
        entity.HasIndex(x => x.ExternalGroupId);
        entity.HasIndex(x => new { x.RoleId, x.IsEnabled });
    }

    private static void ConfigureDomainUserPermissionMapping(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DomainUserPermissionMappingEntity> entity)
    {
        entity.ToTable("DomainUserPermissionMappings");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.UserProfileId);
        entity.Property(x => x.PermissionId).IsRequired();
        entity.Property(x => x.ExternalSubject).HasMaxLength(160);
        entity.Property(x => x.Email).HasMaxLength(320);
        entity.Property(x => x.Reason).HasMaxLength(500);
        entity.HasIndex(x => x.UserProfileId);
        entity.HasIndex(x => x.ExternalSubject);
        entity.HasIndex(x => x.Email);
        entity.HasIndex(x => new { x.PermissionId, x.IsEnabled });
        entity.HasIndex(x => new { x.UserProfileId, x.PermissionId, x.ExternalSubject, x.Email }).IsUnique();
    }
}
