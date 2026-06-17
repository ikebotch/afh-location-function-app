using AFH.Identity.Application.Abstractions;
using AFH.Identity.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Identity.Infrastructure.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IIdentityCurrentUserService, LocationIdentityCurrentUserService>();
        services.AddScoped<IIdentityRbacAdminService, IdentityRbacAdminService>();
        return services;
    }
}
