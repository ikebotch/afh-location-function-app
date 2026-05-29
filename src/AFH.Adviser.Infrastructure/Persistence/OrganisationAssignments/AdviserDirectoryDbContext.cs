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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganisationAssignmentEntity>(ConfigureOrganisationAssignment);
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
}
