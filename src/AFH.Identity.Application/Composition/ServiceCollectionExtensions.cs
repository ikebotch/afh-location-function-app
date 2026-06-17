using Microsoft.Extensions.DependencyInjection;

namespace AFH.Identity.Application.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        return services;
    }
}
