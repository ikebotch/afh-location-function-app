using AFH.Adviser.Application.Abstractions.OrganisationAssignments;
using AFH.Adviser.Application.Abstractions.Feed;
using AFH.Adviser.Application.Abstractions.Repositories;
using AFH.Adviser.Infrastructure.Composition;
using AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;
using AFH.Adviser.Infrastructure.Persistence.Repositories;
using AFH.Location.Application.Abstractions.Geo;
using AFH.Location.Infrastructure.Composition;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Infrastructure.Persistence.Repositories;
using AFH.Location.Function.Functions.V1.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AFH.Location.Tests;

public sealed class InfrastructureCompositionTests
{
    [Fact]
    public void LocationFunctionInfrastructure_RegistersPolicyDbContextForLocationAndAdviserSqlServices()
    {
        var configuration = CreateFunctionStyleConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment());
        services.AddLogging();

        services.AddLocationInfrastructure(configuration);
        services.AddAdviserInfrastructure(configuration);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });

        using var scope = provider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        Assert.NotNull(scopedProvider.GetRequiredService<LocationPolicyDbContext>());
        Assert.NotNull(scopedProvider.GetRequiredService<IDbContextFactory<LocationPolicyDbContext>>());
        Assert.IsType<SqlRouteCache>(scopedProvider.GetRequiredService<IRouteCache>());
        Assert.IsType<SqlAdviserReferenceCacheRepository>(scopedProvider.GetRequiredService<IAdviserReferenceCacheRepository>());
        Assert.IsType<SqlEffectiveCoveragePolicyResolver>(scopedProvider.GetRequiredService<IEffectiveCoveragePolicyResolver>());
        Assert.IsType<SqlOrganisationAssignmentDirectory>(scopedProvider.GetRequiredService<IOrganisationAssignmentDirectory>());
        Assert.NotNull(scopedProvider.GetRequiredService<IOrganisationAssignmentAdminService>());
        Assert.NotNull(scopedProvider.GetRequiredService<IAdviserScopedOrganisationAssignmentResolver>());
    }

    [Fact]
    public void AdviserInfrastructureWithoutLocationPolicyDbContext_FailsWhenSqlAdviserCacheIsResolved()
    {
        var configuration = CreateFunctionStyleConfiguration(includeLocationPolicyDb: false);
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment());
        services.AddLogging();

        services.AddAdviserInfrastructure(configuration);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });

        using var scope = provider.CreateScope();
        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<IAdviserReferenceCacheRepository>());

        Assert.Contains(nameof(LocationPolicyDbContext), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OrganisationAssignmentsFunction_UsesAdminServiceInsteadOfDirectory()
    {
        var constructor = Assert.Single(typeof(OrganisationAssignmentsFunctionV1).GetConstructors());
        var parameterTypes = constructor.GetParameters().Select(x => x.ParameterType).ToArray();

        Assert.Contains(typeof(IOrganisationAssignmentAdminService), parameterTypes);
        Assert.DoesNotContain(typeof(IOrganisationAssignmentDirectory), parameterTypes);
    }

    private static IConfiguration CreateFunctionStyleConfiguration(bool includeLocationPolicyDb = true)
    {
        var values = new Dictionary<string, string?>
        {
                ["ConnectionStrings:AdviserDirectoryDb"] = "Server=(localdb)\\mssqllocaldb;Database=AFHAdviserCompositionTests;Trusted_Connection=True;TrustServerCertificate=True",
                ["ApplicationLogging:Provider"] = "Both",
                ["ApplicationLogging:MaxPayloadLength"] = "2048",
                ["ApplicationLogging:LogPayloads"] = "false",
                ["InternalApiAuth:Token"] = "test-token",
                ["InternalApiAuth:AllowAnonymousInDevelopment"] = "false",
                ["LocationSearch:Coverage:AverageTravelSpeedMph"] = "30",
                ["LocationSearch:Coverage:DefaultRadiusMiles"] = "100",
                ["LocationSearch:Coverage:DefaultMaxTravelTimeMinutes"] = "90",
                ["LocationSearch:PolicyStore:AutoCreate"] = "false",
                ["LocationSearch:PolicyStore:SeedFromConfiguration"] = "false",
                ["Maps:Google:Enabled"] = "false",
                ["CalendarService:BaseUrl"] = "http://localhost:7071",
                ["CalendarService:ScheduleLookbackMinutes"] = "360",
                ["AdviserFeed:Enabled"] = "true",
                ["AdviserFeed:BaseUrl"] = "http://localhost:7073",
                ["AzureAD:TenantId"] = "tenant-id",
                ["AzureAD:ClientId"] = "client-id",
                ["AzureAD:ClientSecret"] = "client-secret",
                ["AzureAD:AuthorityHost"] = "https://login.microsoftonline.com/",
                ["AzureAD:Scopes:0"] = "https://graph.microsoft.com/.default"
        };

        if (includeLocationPolicyDb)
        {
            values["ConnectionStrings:LocationPolicyDb"] =
                "Server=(localdb)\\mssqllocaldb;Database=AFHLocationCompositionTests;Trusted_Connection=True;TrustServerCertificate=True";
        }

        return TestConfiguration.Create(values);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "AFH.Location.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
