using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;


namespace AFH.Location.Service.Infrastructure.Persistence.PolicyStore;

/// <summary>
/// Design-time factory used by EF Core tools without bootstrapping the Functions host.
/// </summary>
public sealed class LocationPolicyDbContextFactory : IDesignTimeDbContextFactory<LocationPolicyDbContext>
{
    public LocationPolicyDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("local.settings.json", optional: true)
            .AddJsonFile(Path.Combine("src", "AFH.Location.Service.Api", "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine("src", "AFH.Location.Service.Api", "local.settings.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            config.GetConnectionString("LocationPolicyDb")
            ?? config["ConnectionStrings:LocationPolicyDb"]
            ?? config["Values:ConnectionStrings:LocationPolicyDb"]
            ?? config["LocationSearch:PolicyStore:ConnectionString"]
            ?? config["Values:LocationSearch:PolicyStore:ConnectionString"];

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