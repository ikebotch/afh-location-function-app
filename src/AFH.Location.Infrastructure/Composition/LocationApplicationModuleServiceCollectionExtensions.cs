using Microsoft.Extensions.DependencyInjection;

namespace AFH.Location.Infrastructure.Composition;

internal static class LocationApplicationModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddLocationApplicationModule(this IServiceCollection services)
    {
        return services;
    }
}
