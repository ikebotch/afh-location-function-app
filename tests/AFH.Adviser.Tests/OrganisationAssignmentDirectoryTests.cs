using AFH.Adviser.Application.Models.Auth;
using AFH.Adviser.Application.Models.OrganisationAssignments;
using AFH.Identity.Infrastructure.Persistence;
using AFH.Identity.Infrastructure.Persistence.Entities;
using AFH.Identity.Infrastructure.Options;
using AFH.Adviser.Contract.V1.OrganisationAssignments;
using AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFH.Adviser.Tests;

public sealed class OrganisationAssignmentDirectoryTests
{
    [Fact]
    public async Task SearchAsync_ReturnsEnabledContactsByNeutralRoleTerms()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await directory.CreateAsync(new OrganisationAssignmentUpsert(
            "Booking",
            "Manager",
            null,
            null,
            "North",
            null,
            "Regional Manager",
            "manager@example.com",
            null,
            ["Email"],
            IsEnabled: true,
            Priority: 10), CancellationToken.None);
        await directory.CreateAsync(new OrganisationAssignmentUpsert(
            "Booking",
            "Manager",
            null,
            null,
            "South",
            null,
            "Disabled Manager",
            "disabled@example.com",
            null,
            ["Email"],
            IsEnabled: false,
            Priority: 1), CancellationToken.None);

        var assignments = await directory.SearchAsync(new OrganisationAssignmentSearch(
            "Booking",
            ["Manager"],
            null,
            null,
            "North",
            null), CancellationToken.None);

        var assignment = Assert.Single(assignments);
        Assert.Equal("Manager", assignment.AssignmentType);
        Assert.Equal("manager@example.com", assignment.Email);
        Assert.DoesNotContain("notification", string.Join(' ', typeof(OrganisationAssignmentDto).GetProperties().Select(x => x.Name)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recipient", string.Join(' ', typeof(OrganisationAssignmentDto).GetProperties().Select(x => x.Name)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dispatch", string.Join(' ', typeof(OrganisationAssignmentDto).GetProperties().Select(x => x.Name)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateDisableAndDelete_AreDbDriven()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        var created = await directory.CreateAsync(new OrganisationAssignmentUpsert(
            "Booking",
            "ContactCentre",
            null,
            null,
            null,
            null,
            "Contact Centre",
            "assignment@example.com",
            null,
            ["Email"],
            IsEnabled: true,
            Priority: 20), CancellationToken.None);

        var updated = await directory.UpdateAsync(created.Id, new OrganisationAssignmentUpsert(
            "Booking",
            "ContactCentre",
            null,
            null,
            null,
            null,
            "Contact Centre",
            "updated@example.com",
            null,
            ["Email"],
            IsEnabled: true,
            Priority: 5), CancellationToken.None);
        Assert.Equal("updated@example.com", updated?.Email);

        Assert.True(await directory.DisableAsync(created.Id, CancellationToken.None));
        var enabled = await directory.SearchAsync(new OrganisationAssignmentSearch("Booking", ["ContactCentre"], null, null, null, null), CancellationToken.None);
        Assert.Empty(enabled);

        var disabled = await directory.SearchAsync(new OrganisationAssignmentSearch("Booking", ["ContactCentre"], null, null, null, null, IncludeDisabled: true), CancellationToken.None);
        Assert.Single(disabled);

        Assert.True(await directory.DeleteAsync(created.Id, CancellationToken.None));
        Assert.Null(await directory.GetAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_WithoutScope_ReturnsAllMatchingAdminRows()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await directory.CreateAsync(new OrganisationAssignmentUpsert(
            "Booking",
            "ContactCentre",
            "org-1",
            null,
            "Region A",
            null,
            "Region A Contact Centre",
            "region-a@example.com",
            null,
            ["Email"],
            IsEnabled: true,
            Priority: 10), CancellationToken.None);
        await directory.CreateAsync(new OrganisationAssignmentUpsert(
            "Booking",
            "ContactCentre",
            "org-1",
            null,
            "Region B",
            null,
            "Region B Contact Centre",
            "region-b@example.com",
            null,
            ["Email"],
            IsEnabled: true,
            Priority: 20), CancellationToken.None);

        var assignments = await directory.SearchAsync(new OrganisationAssignmentSearch(
            "Booking",
            ["ContactCentre"],
            OrganisationId: null,
            ClientId: null,
            Region: null,
            AdviserId: null), CancellationToken.None);

        Assert.Collection(assignments,
            first => Assert.Equal("Region A Contact Centre", first.DisplayName),
            second => Assert.Equal("Region B Contact Centre", second.DisplayName));
    }

    [Fact]
    public async Task ResolveScopedAsync_AdviserMatchWins()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await AddAssignmentAsync(directory, "Regional Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A", priority: 1);
        await AddAssignmentAsync(directory, "Adviser Contact Centre", "ContactCentre", adviserId: "adv-1", priority: 100);

        var matches = await directory.ResolveScopedAsync(ScopedSearch("adv-1", "org-1", "Region A"), CancellationToken.None);

        var match = Assert.Single(matches);
        Assert.Equal("Adviser Contact Centre", match.Assignment.DisplayName);
        Assert.Equal(OrganisationAssignmentMatchLevels.Adviser, match.MatchLevel);
        Assert.Equal("adv-1", match.MatchedAdviserId);
    }

    [Fact]
    public async Task ResolveScopedAsync_OrganisationRegionBeatsOrganisationOnly()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await AddAssignmentAsync(directory, "Org Contact Centre", "ContactCentre", organisationId: "org-1", priority: 1);
        await AddAssignmentAsync(directory, "Regional Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A", priority: 100);

        var matches = await directory.ResolveScopedAsync(ScopedSearch("adv-1", "org-1", "Region A"), CancellationToken.None);

        var match = Assert.Single(matches);
        Assert.Equal("Regional Contact Centre", match.Assignment.DisplayName);
        Assert.Equal(OrganisationAssignmentMatchLevels.OrganisationRegion, match.MatchLevel);
    }

    [Fact]
    public async Task ResolveScopedAsync_OrganisationOnlyAppliesAcrossRegions()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await AddAssignmentAsync(directory, "Org Contact Centre", "ContactCentre", organisationId: "org-1");

        var matches = await directory.ResolveScopedAsync(ScopedSearch("adv-1", "org-1", "Region B"), CancellationToken.None);

        var match = Assert.Single(matches);
        Assert.Equal("Org Contact Centre", match.Assignment.DisplayName);
        Assert.Equal(OrganisationAssignmentMatchLevels.Organisation, match.MatchLevel);
        Assert.Equal("org-1", match.MatchedOrganisationId);
        Assert.Null(match.MatchedRegion);
    }

    [Fact]
    public async Task ResolveScopedAsync_RegionAAssignmentDoesNotMatchRegionBAdviser()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await AddAssignmentAsync(directory, "Region A Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A");

        var matches = await directory.ResolveScopedAsync(ScopedSearch("adv-1", "org-1", "Region B"), CancellationToken.None);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task ResolveScopedAsync_GlobalAssignmentAppliesWhenNoSpecificMatchExists()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await AddAssignmentAsync(directory, "Global Contact Centre", "ContactCentre");

        var matches = await directory.ResolveScopedAsync(ScopedSearch("adv-1", "org-1", "Region A"), CancellationToken.None);

        var match = Assert.Single(matches);
        Assert.Equal("Global Contact Centre", match.Assignment.DisplayName);
        Assert.Equal(OrganisationAssignmentMatchLevels.Global, match.MatchLevel);
        Assert.Null(match.MatchedOrganisationId);
        Assert.Null(match.MatchedRegion);
        Assert.Null(match.MatchedAdviserId);
    }

    [Fact]
    public async Task ResolveScopedAsync_GlobalAssignmentDoesNotOverrideAdviserSpecificMatch()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await AddAssignmentAsync(directory, "Global Contact Centre", "ContactCentre", priority: 1);
        await AddAssignmentAsync(directory, "Adviser Contact Centre", "ContactCentre", adviserId: "adv-1", priority: 100);

        var matches = await directory.ResolveScopedAsync(ScopedSearch("adv-1", "org-1", "Region A"), CancellationToken.None);

        var match = Assert.Single(matches);
        Assert.Equal("Adviser Contact Centre", match.Assignment.DisplayName);
        Assert.Equal(OrganisationAssignmentMatchLevels.Adviser, match.MatchLevel);
    }

    [Fact]
    public async Task ResolveScopedAsync_GlobalAssignmentDoesNotOverrideOrganisationRegionMatch()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await AddAssignmentAsync(directory, "Global Contact Centre", "ContactCentre", priority: 1);
        await AddAssignmentAsync(directory, "Regional Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A", priority: 100);

        var matches = await directory.ResolveScopedAsync(ScopedSearch("adv-1", "org-1", "Region A"), CancellationToken.None);

        var match = Assert.Single(matches);
        Assert.Equal("Regional Contact Centre", match.Assignment.DisplayName);
        Assert.Equal(OrganisationAssignmentMatchLevels.OrganisationRegion, match.MatchLevel);
    }

    [Fact]
    public async Task ResolveScopedAsync_GlobalAssignmentResolvesIndependentlyPerAssignmentType()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await AddAssignmentAsync(directory, "Regional Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A");
        await AddAssignmentAsync(directory, "Global Operations Manager", "OperationsManager");

        var matches = await directory.ResolveScopedAsync(
            ScopedSearch("adv-1", "org-1", "Region A", ["ContactCentre", "OperationsManager"]),
            CancellationToken.None);

        Assert.Collection(
            matches,
            first =>
            {
                Assert.Equal("Regional Contact Centre", first.Assignment.DisplayName);
                Assert.Equal(OrganisationAssignmentMatchLevels.OrganisationRegion, first.MatchLevel);
            },
            second =>
            {
                Assert.Equal("Global Operations Manager", second.Assignment.DisplayName);
                Assert.Equal(OrganisationAssignmentMatchLevels.Global, second.MatchLevel);
            });
    }

    [Fact]
    public async Task ResolveScopedAsync_ExcludesDisabledAssignments()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await AddAssignmentAsync(directory, "Disabled Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A", isEnabled: false);

        var matches = await directory.ResolveScopedAsync(ScopedSearch("adv-1", "org-1", "Region A"), CancellationToken.None);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task ResolveScopedAsync_ExcludesDisabledGlobalAssignments()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await AddAssignmentAsync(directory, "Disabled Global Contact Centre", "ContactCentre", isEnabled: false);

        var matches = await directory.ResolveScopedAsync(ScopedSearch("adv-1", "org-1", "Region A"), CancellationToken.None);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task ResolveScopedAsync_FallbackOnlyWhenNoSpecificMatchExists()
    {
        await using var db = CreateDb();
        var directory = new SqlOrganisationAssignmentDirectory(db);
        await AddAssignmentAsync(directory, "Regional Contact Centre", "ContactCentre", organisationId: "org-1", region: "Region A");
        await AddAssignmentAsync(directory, "Fallback Team", "Fallback");

        var specificMatches = await directory.ResolveScopedAsync(
            ScopedSearch("adv-1", "org-1", "Region A", ["ContactCentre", "Fallback"], includeFallback: true),
            CancellationToken.None);

        var specificMatch = Assert.Single(specificMatches);
        Assert.Equal("Regional Contact Centre", specificMatch.Assignment.DisplayName);

        var fallbackMatches = await directory.ResolveScopedAsync(
            ScopedSearch("adv-1", "org-2", "Region B", ["ContactCentre", "Fallback"], includeFallback: true),
            CancellationToken.None);

        var fallbackMatch = Assert.Single(fallbackMatches);
        Assert.Equal("Fallback Team", fallbackMatch.Assignment.DisplayName);
        Assert.Equal(OrganisationAssignmentMatchLevels.Fallback, fallbackMatch.MatchLevel);
    }

    [Fact]
    public async Task UserContextStore_ReturnsAllRolesAndPermissionsForMappedUser()
    {
        await using var db = CreateIdentityDb();
        var managerRoleId = Guid.NewGuid();
        var adminRoleId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var approvalsReadPermissionId = Guid.NewGuid();
        var approvalsReviewPermissionId = Guid.NewGuid();
        var approvalRequestsCreatePermissionId = Guid.NewGuid();
        db.DomainUserProfiles.Add(new DomainUserProfileEntity
        {
            Id = profileId,
            ExternalSubject = "user-1",
            Email = "alex@afh.co.uk",
            DisplayName = "Alex Example",
            AdviserId = "adv-1",
            Status = "Active",
            CreatedUtc = DateTime.UtcNow
        });
        db.DomainRoles.AddRange(
            new DomainRoleEntity
            {
                Id = managerRoleId,
                Role = "Manager",
                CreatedUtc = DateTime.UtcNow
            },
            new DomainRoleEntity
            {
                Id = adminRoleId,
                Role = "Admin",
                CreatedUtc = DateTime.UtcNow
            });
        db.DomainPermissions.AddRange(
            Permission(approvalsReadPermissionId, BookingPermissionNames.ApprovalsRead),
            Permission(approvalsReviewPermissionId, BookingPermissionNames.ApprovalsReview),
            Permission(approvalRequestsCreatePermissionId, BookingPermissionNames.ApprovalRequestsCreate));
        db.DomainUserRoleMappings.AddRange(
            new DomainUserRoleMappingEntity
            {
                Id = Guid.NewGuid(),
                UserProfileId = profileId,
                RoleId = managerRoleId,
                Email = "alex@afh.co.uk",
                IsEnabled = true,
                CreatedUtc = DateTime.UtcNow
            },
            new DomainUserRoleMappingEntity
            {
                Id = Guid.NewGuid(),
                RoleId = adminRoleId,
                ExternalRole = "BookingAdmin",
                IsEnabled = true,
                CreatedUtc = DateTime.UtcNow
            });
        db.DomainRolePermissions.AddRange(
            new DomainRolePermissionEntity
            {
                Id = Guid.NewGuid(),
                RoleId = managerRoleId,
                PermissionId = approvalsReadPermissionId,
                CreatedUtc = DateTime.UtcNow
            },
            new DomainRolePermissionEntity
            {
                Id = Guid.NewGuid(),
                RoleId = adminRoleId,
                PermissionId = approvalsReviewPermissionId,
                CreatedUtc = DateTime.UtcNow
            },
            new DomainRolePermissionEntity
            {
                Id = Guid.NewGuid(),
                RoleId = managerRoleId,
                PermissionId = approvalRequestsCreatePermissionId,
                CreatedUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var store = new SqlDomainUserContextStore(db, DefaultRbacOptions());
        var context = await store.GetContextAsync(
            new DomainUserIdentity(
                "user-1",
                "alex@afh.co.uk",
                "Alex Example",
                ["BookingAdmin"],
                []),
            CancellationToken.None);

        Assert.Equal(profileId.ToString("D"), context.UserId);
        Assert.Equal("user-1", context.ExternalSubject);
        Assert.Equal("alex@afh.co.uk", context.Email);
        Assert.Equal("Alex Example", context.DisplayName);
        Assert.Equal("adv-1", context.AdviserId);
        Assert.Contains("Manager", context.Roles);
        Assert.Contains("Admin", context.Roles);
        Assert.Contains(BookingPermissionNames.ApprovalsRead, context.Permissions);
        Assert.Contains(BookingPermissionNames.ApprovalsReview, context.Permissions);
        Assert.Contains(BookingPermissionNames.ApprovalRequestsCreate, context.Permissions);
    }

    [Fact]
    public async Task UserPermissionStore_AllowsRolePermissionThroughDomainProfile()
    {
        await using var db = CreateIdentityDb();
        var roleId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        db.DomainUserProfiles.Add(new DomainUserProfileEntity
        {
            Id = profileId,
            ExternalSubject = "user-1",
            Email = "alex@afh.co.uk",
            DisplayName = "Alex Example",
            AdviserId = "adv-1",
            Status = "Active",
            CreatedUtc = DateTime.UtcNow
        });
        db.DomainRoles.Add(new DomainRoleEntity
        {
            Id = roleId,
            Role = "Adviser",
            CreatedUtc = DateTime.UtcNow
        });
        db.DomainPermissions.Add(Permission(permissionId, BookingPermissionNames.ApprovalRequestsCreate));
        db.DomainUserRoleMappings.Add(new DomainUserRoleMappingEntity
        {
            Id = Guid.NewGuid(),
            UserProfileId = profileId,
            RoleId = roleId,
            IsEnabled = true,
            CreatedUtc = DateTime.UtcNow
        });
        db.DomainRolePermissions.Add(new DomainRolePermissionEntity
        {
            Id = Guid.NewGuid(),
            RoleId = roleId,
            PermissionId = permissionId,
            CreatedUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var store = new SqlDomainUserPermissionStore(db, DefaultRbacOptions());
        var allowed = await store.HasPermissionAsync(
            new DomainUserIdentity(
                "user-1",
                "alex@afh.co.uk",
                "Alex Example",
                [],
                []),
            BookingPermissionNames.ApprovalRequestsCreate,
            CancellationToken.None);

        Assert.True(allowed);
    }

    [Fact]
    public async Task UserPermissionStore_DirectDenyOverridesRolePermission()
    {
        await using var db = CreateIdentityDb();
        var roleId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        db.DomainUserProfiles.Add(new DomainUserProfileEntity
        {
            Id = profileId,
            ExternalSubject = "user-1",
            Email = "alex@afh.co.uk",
            DisplayName = "Alex Example",
            CreatedUtc = DateTime.UtcNow
        });
        db.DomainRoles.Add(new DomainRoleEntity { Id = roleId, Role = "Adviser", CreatedUtc = DateTime.UtcNow });
        db.DomainPermissions.Add(Permission(permissionId, BookingPermissionNames.ApprovalRequestsCreate));
        db.DomainUserRoleMappings.Add(new DomainUserRoleMappingEntity
        {
            Id = Guid.NewGuid(),
            UserProfileId = profileId,
            RoleId = roleId,
            IsEnabled = true,
            CreatedUtc = DateTime.UtcNow
        });
        db.DomainRolePermissions.Add(new DomainRolePermissionEntity
        {
            Id = Guid.NewGuid(),
            RoleId = roleId,
            PermissionId = permissionId,
            CreatedUtc = DateTime.UtcNow
        });
        db.DomainUserPermissionMappings.Add(new DomainUserPermissionMappingEntity
        {
            Id = Guid.NewGuid(),
            UserProfileId = profileId,
            PermissionId = permissionId,
            IsGranted = false,
            IsEnabled = true,
            CreatedUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var store = new SqlDomainUserPermissionStore(db, DefaultRbacOptions());
        var allowed = await store.HasPermissionAsync(
            new DomainUserIdentity("user-1", "alex@afh.co.uk", "Alex Example", [], []),
            BookingPermissionNames.ApprovalRequestsCreate,
            CancellationToken.None);

        Assert.False(allowed);
    }

    [Fact]
    public async Task UserPermissionStore_DirectGrantAllowsPermissionWithoutRole()
    {
        await using var db = CreateIdentityDb();
        var profileId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        db.DomainUserProfiles.Add(new DomainUserProfileEntity
        {
            Id = profileId,
            ExternalSubject = "user-1",
            Email = "alex@afh.co.uk",
            DisplayName = "Alex Example",
            CreatedUtc = DateTime.UtcNow
        });
        db.DomainPermissions.Add(Permission(permissionId, BookingPermissionNames.ApprovalRequestsCreate));
        db.DomainUserPermissionMappings.Add(new DomainUserPermissionMappingEntity
        {
            Id = Guid.NewGuid(),
            UserProfileId = profileId,
            PermissionId = permissionId,
            IsGranted = true,
            IsEnabled = true,
            CreatedUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var store = new SqlDomainUserPermissionStore(db, DefaultRbacOptions());
        var allowed = await store.HasPermissionAsync(
            new DomainUserIdentity("user-1", "alex@afh.co.uk", "Alex Example", [], []),
            BookingPermissionNames.ApprovalRequestsCreate,
            CancellationToken.None);

        Assert.True(allowed);
    }

    private static DomainPermissionEntity Permission(Guid id, string permission) => new()
    {
        Id = id,
        Permission = permission,
        DisplayName = permission.Replace(".", " ", StringComparison.Ordinal),
        Category = permission.Split('.')[0],
        IsEnabled = true,
        CreatedUtc = DateTime.UtcNow
    };

    private static AdviserDirectoryDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AdviserDirectoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AdviserDirectoryDbContext(options);
    }

    private static IdentityDbContext CreateIdentityDb()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new IdentityDbContext(options);
    }

    private static IOptions<IdentityRbacOptions> DefaultRbacOptions() =>
        Options.Create(new IdentityRbacOptions());

    private static OrganisationAssignmentScopedSearch ScopedSearch(
        string adviserId,
        string? organisationId,
        string? region,
        IReadOnlyList<string>? assignmentTypes = null,
        bool includeFallback = false)
        => new(
            "Booking",
            assignmentTypes ?? ["ContactCentre"],
            adviserId,
            organisationId,
            region,
            ClientId: null,
            IncludeDisabled: false,
            IncludeFallback: includeFallback);

    private static async Task AddAssignmentAsync(
        SqlOrganisationAssignmentDirectory directory,
        string displayName,
        string assignmentType,
        string? organisationId = null,
        string? region = null,
        string? adviserId = null,
        bool isEnabled = true,
        int priority = 100)
        => await directory.CreateAsync(new OrganisationAssignmentUpsert(
            "Booking",
            assignmentType,
            organisationId,
            ClientId: null,
            region,
            adviserId,
            displayName,
            $"{displayName.Replace(" ", ".", StringComparison.OrdinalIgnoreCase).ToLowerInvariant()}@example.test",
            MobileNumber: null,
            ["Email"],
            isEnabled,
            priority), CancellationToken.None);
}
