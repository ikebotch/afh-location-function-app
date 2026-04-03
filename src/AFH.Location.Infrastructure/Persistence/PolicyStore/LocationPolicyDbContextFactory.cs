using AFH.Location.Infrastructure.Composition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;


namespace AFH.Location.Infrastructure.Persistence.PolicyStore;


public sealed class LocationPolicyDbContextFactory : IDesignTimeDbContextFactory<LocationPolicyDbContext>
{
    public LocationPolicyDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("local.settings.json", optional: true)
            .AddJsonFile(Path.Combine("src", "AFH.Location.Function", "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine("src", "AFH.Location.Function", "local.settings.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = DependencyInjection.ResolveLocationPolicyDbConnectionString(config);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing SQL connection string for LocationPolicyDbContext. " +
                "Set ConnectionStrings:LocationPolicyDb (or LocationSearch:PolicyStore:ConnectionString).");
        }

        var optionsBuilder = new DbContextOptionsBuilder<LocationPolicyDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new LocationPolicyDbContext(optionsBuilder.Options);
    }
}
