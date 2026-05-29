using AFH.Adviser.Infrastructure.Composition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AFH.Adviser.Infrastructure.Persistence.OrganisationAssignments;

public sealed class AdviserDirectoryDbContextFactory : IDesignTimeDbContextFactory<AdviserDirectoryDbContext>
{
    public AdviserDirectoryDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("local.settings.json", optional: true)
            .AddJsonFile(Path.Combine("src", "AFH.Location.Function", "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine("src", "AFH.Location.Function", "local.settings.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = AdviserInfrastructureDependencyInjection.ResolveAdviserDirectoryDbConnectionString(config);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing SQL connection string for AdviserDirectoryDbContext. " +
                "Set ConnectionStrings:AdviserDirectoryDb.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AdviserDirectoryDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new AdviserDirectoryDbContext(optionsBuilder.Options);
    }
}
