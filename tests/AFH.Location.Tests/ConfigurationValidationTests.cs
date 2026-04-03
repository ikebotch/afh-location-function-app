using AFH.Location.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace AFH.Location.Tests;

public sealed class ConfigurationValidationTests
{
    [Fact]
    public void InternalApiAuthValidator_RequiresTokenOutsideAnonymousDevelopment()
    {
        var validator = new InternalApiAuthOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(null, new InternalApiAuthOptions());

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveLocationPolicyDbConnectionString_PrefersConnectionStringsThenLegacyFallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LocationPolicyDb"] = "Server=cs;",
                ["LocationSearch:PolicyStore:ConnectionString"] = "Server=legacy;"
            })
            .Build();

        var connectionString = typeof(AFH.Location.Infrastructure.Composition.DependencyInjection)
            .GetMethod("ResolveLocationPolicyDbConnectionString", BindingFlags.Static | BindingFlags.NonPublic)?
            .Invoke(null, [configuration]) as string;

        Assert.Equal("Server=cs;", connectionString);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "AFH.Location.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
