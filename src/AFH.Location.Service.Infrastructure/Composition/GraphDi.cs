using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class GraphDi
{
    public static IServiceCollection AddGraphClient(this IServiceCollection services, IConfiguration config)
    {
        var tenantId = config["SharePointGraph:TenantId"] ?? config["AzureAd:TenantId"];
        var clientId = config["SharePointGraph:ClientId"] ?? config["AzureAd:ClientId"];
        var clientSecret = config["SharePointGraph:ClientSecret"] ?? config["AzureAd:ClientSecret"];

        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "Graph auth config is missing. Set SharePointGraph:TenantId/ClientId/ClientSecret (or AzureAd:TenantId/ClientId/ClientSecret).");
        }

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var scopes = new[] { "https://graph.microsoft.com/.default" };

        services.AddSingleton(new GraphServiceClient(credential, scopes));

        return services;
    }
}
