using AFH.Identity.Infrastructure.Composition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AFH.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("local.settings.json", optional: true)
            .AddJsonFile(Path.Combine("src", "AFH.Location.Function", "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine("src", "AFH.Location.Function", "local.settings.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = ServiceCollectionExtensions.ResolveIdentityDbConnectionString(config);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing SQL connection string for IdentityDbContext. " +
                "Set ConnectionStrings:IdentityDb or ConnectionStrings:AdviserDirectoryDb.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new IdentityDbContext(optionsBuilder.Options);
    }
}
