using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class GraphDi
{
    public static IServiceCollection AddGraphClient(this IServiceCollection services, IConfiguration config)
    {
        var tenantId = config["SharePointGraph:TenantId"];
        var clientId = config["SharePointGraph:ClientId"];
        var clientSecret = config["SharePointGraph:ClientSecret"];

        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("AzureAd:TenantId/ClientId/ClientSecret are required for Graph client secret auth.");

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var scopes = new[] { "https://graph.microsoft.com/.default" };

        services.AddSingleton(new GraphServiceClient(credential, scopes));

        return services;
    }
}