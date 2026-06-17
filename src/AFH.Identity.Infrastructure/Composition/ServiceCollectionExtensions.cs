using AFH.Adviser.Application.Abstractions.Auth;
using AFH.Identity.Application.Abstractions;
using AFH.Identity.Infrastructure.Persistence;
using AFH.Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Identity.Infrastructure.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var identityConnectionString = ResolveIdentityDbConnectionString(configuration);
        if (string.IsNullOrWhiteSpace(identityConnectionString))
        {
            throw new InvalidOperationException(
                "Missing SQL connection string for IdentityDbContext. " +
                "Set ConnectionStrings:IdentityDb or ConnectionStrings:AdviserDirectoryDb.");
        }

        services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(identityConnectionString));
        services.AddHostedService<IdentityDbInitializer>();
        services.AddScoped<IDomainUserPermissionStore, SqlDomainUserPermissionStore>();
        services.AddScoped<IDomainUserContextStore, SqlDomainUserContextStore>();
        services.AddScoped<IIdentityCurrentUserService, LocationIdentityCurrentUserService>();
        services.AddScoped<IIdentityRbacAdminService, IdentityRbacAdminService>();
        return services;
    }

    public static string? ResolveIdentityDbConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("IdentityDb")
        ?? configuration["ConnectionStrings:IdentityDb"]
        ?? configuration["Values:ConnectionStrings:IdentityDb"]
        ?? configuration.GetConnectionString("AdviserDirectoryDb")
        ?? configuration["ConnectionStrings:AdviserDirectoryDb"]
        ?? configuration["Values:ConnectionStrings:AdviserDirectoryDb"]
        ?? configuration.GetConnectionString("LocationPolicyDb")
        ?? configuration["ConnectionStrings:LocationPolicyDb"]
        ?? configuration["Values:ConnectionStrings:LocationPolicyDb"]
        ?? configuration["LocationSearch:PolicyStore:ConnectionString"]
        ?? configuration["Values:LocationSearch:PolicyStore:ConnectionString"];
}
