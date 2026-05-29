using AFH.Common.Errors.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Reflection;

namespace AFH.Location.Tests;

public sealed class ArchitectureGuardTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Application_Infrastructure_And_Domain_DoNotReferenceFunctionAssembly()
    {
        AssertDoesNotReference("AFH.Location.Application", "AFH.Location.Function");
        AssertDoesNotReference("AFH.Location.Infrastructure", "AFH.Location.Function");
        AssertDoesNotReference("AFH.Location.Domain", "AFH.Location.Function");
        AssertDoesNotReference("AFH.Location.Contract", "AFH.Location.Function");
    }

    [Fact]
    public void Adviser_And_Location_ApplicationDomainBoundaries_DoNotCross()
    {
        AssertDoesNotReference("AFH.Location.Application", "AFH.Adviser.Application");
        AssertDoesNotReference("AFH.Location.Application", "AFH.Adviser.Domain");
        AssertDoesNotReference("AFH.Location.Domain", "AFH.Adviser.Application");
        AssertDoesNotReference("AFH.Location.Domain", "AFH.Adviser.Domain");

        AssertDoesNotReference("AFH.Adviser.Application", "AFH.Location.Application");
        AssertDoesNotReference("AFH.Adviser.Application", "AFH.Location.Domain");
        AssertDoesNotReference("AFH.Adviser.Domain", "AFH.Location.Application");
        AssertDoesNotReference("AFH.Adviser.Domain", "AFH.Location.Domain");
    }

    [Fact]
    public void LocationProjects_DoNotOwnOrganisationAssignmentsOrDomainRbac()
    {
        var forbiddenTerms = new[]
        {
            "OrganisationAssignment",
            "OrganisationAssignments",
            "DomainRole",
            "DomainRoles",
            "DomainUserRoleMapping",
            "DomainUserRoleMappings",
            "DomainRolePermission",
            "DomainRolePermissions",
            "DomainUserPermissionStore",
            "OrganisationAssignmentPermissions"
        };

        var allowedFunctionWrapperFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine("src", "AFH.Location.Function", "Functions", "V1", "Admin", "OrganisationAssignmentsFunctionV1.cs")
        };

        var violations = EnumerateSourceFiles(
                "src/AFH.Location.Application",
                "src/AFH.Location.Contract",
                "src/AFH.Location.Domain",
                "src/AFH.Location.Infrastructure",
                "src/AFH.Location.Function")
            .Where(path => !allowedFunctionWrapperFiles.Contains(Path.GetRelativePath(RepositoryRoot, path)))
            .Select(path => new
            {
                Path = Path.GetRelativePath(RepositoryRoot, path),
                Text = File.ReadAllText(path)
            })
            .Where(file => forbiddenTerms.Any(term => file.Text.Contains(term, StringComparison.Ordinal)))
            .Select(file => file.Path)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Application_Domain_And_Contract_DoNotReferenceSharedErrorSdkAssemblies()
    {
        AssertDoesNotReferencePrefix("AFH.Location.Application", "AFH.Common.Errors");
        AssertDoesNotReferencePrefix("AFH.Location.Domain", "AFH.Common.Errors");
        AssertDoesNotReferencePrefix("AFH.Location.Contract", "AFH.Common.Errors");
    }

    [Fact]
    public void Function_And_Infrastructure_RespectSharedErrorSdkBoundaries()
    {
        AssertReferences("AFH.Location.Function", "AFH.Common.Errors");
        AssertReferences("AFH.Location.Function", "AFH.Common.Errors.AzureFunctions");
        AssertDoesNotReference("AFH.Location.Function", "AFH.Common.Errors.ApplicationInsights");
        AssertDoesNotReference("AFH.Location.Function", "AFH.Common.Errors.Email");
        AssertDoesNotReference("AFH.Location.Function", "AFH.Common.Errors.EntityFramework");

        AssertReferences("AFH.Location.Infrastructure", "AFH.Common.Errors.EntityFramework");
        AssertReferences("AFH.Location.Infrastructure", "AFH.Common.Errors.Email");
        AssertReferences("AFH.Location.Infrastructure", "AFH.Common.Errors.ApplicationInsights");
        AssertDoesNotReference("AFH.Location.Infrastructure", "AFH.Common.Errors.AzureFunctions");
    }

    [Fact]
    public void FunctionAssembly_KeepsExceptionMapperLocal()
    {
        var functionAssembly = Assembly.Load("AFH.Location.Function");

        var mapperTypes = functionAssembly.GetTypes()
            .Where(type => typeof(IExceptionMapper).IsAssignableFrom(type))
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .ToArray();

        var mapper = Assert.Single(mapperTypes);
        Assert.Equal("LocationExceptionMapper", mapper.Name);
        Assert.Equal("AFH.Location.Function.Middleware", mapper.Namespace);
    }

    [Fact]
    public void EndpointAccessPolicies_RequireExplicitHttpFunctionCoverage()
    {
        var functionAssembly = Assembly.Load("AFH.Location.Function");
        var endpointPoliciesType = functionAssembly.GetType("AFH.Location.Function.Security.EndpointAccessPolicies");
        var getPolicy = endpointPoliciesType?.GetMethod("GetPolicy", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(getPolicy);

        foreach (var functionName in GetHttpFunctionNames(functionAssembly))
        {
            var exception = Record.Exception(() => getPolicy!.Invoke(null, [functionName]));
            Assert.Null(exception);
        }

        var thrown = Assert.Throws<TargetInvocationException>(() => getPolicy!.Invoke(null, ["UnmappedHttpFunction"]));
        Assert.IsType<InvalidOperationException>(thrown.InnerException);
    }

    [Fact]
    public void EndpointAccessPolicies_KnownHttpFunctions_ExactlyMatchDiscoveredHttpFunctions()
    {
        var functionAssembly = Assembly.Load("AFH.Location.Function");
        var endpointPoliciesType = functionAssembly.GetType("AFH.Location.Function.Security.EndpointAccessPolicies");
        var knownHttpFunctionsProperty = endpointPoliciesType?.GetProperty("KnownHttpFunctions", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(knownHttpFunctionsProperty);

        var knownHttpFunctions = ((IReadOnlyCollection<string>?)knownHttpFunctionsProperty!.GetValue(null))?
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.NotNull(knownHttpFunctions);
        Assert.Equal(GetHttpFunctionNames(functionAssembly), knownHttpFunctions);
    }

    [Fact]
    public void Program_RemainsAThinBootstrapShell()
    {
        var programText = File.ReadAllText(GetProgramPath("AFH.Location.Function"));

        Assert.Contains("ConfigureMiddlewarePipeline(app);", programText);
        Assert.Contains("ConfigureAppConfiguration(cfg);", programText);
        Assert.Contains("AddSharedErrorHandling(services, ctx.Configuration", programText);
        Assert.Contains("ConfigureWorkerSerialization(services", programText);
        Assert.DoesNotContain("BuildServiceProvider(", programText);
    }

    private static string[] GetHttpFunctionNames(Assembly functionAssembly) =>
        functionAssembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .Where(method => method.GetCustomAttribute<FunctionAttribute>() is not null)
            .Where(method => method.GetParameters().Any(parameter => parameter.GetCustomAttributes<HttpTriggerAttribute>(inherit: false).Any()))
            .Select(method => method.GetCustomAttribute<FunctionAttribute>()!.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static void AssertDoesNotReference(string assemblyName, string forbiddenAssemblyName)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Assert.DoesNotContain(forbiddenAssemblyName, references);
    }

    private static void AssertReferences(string assemblyName, string expectedAssemblyName)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Assert.Contains(expectedAssemblyName, references);
    }

    private static void AssertDoesNotReferencePrefix(string assemblyName, string forbiddenPrefix)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Assert.DoesNotContain(references, reference => reference?.StartsWith(forbiddenPrefix, StringComparison.Ordinal) == true);
    }

    private static string GetProgramPath(string functionProjectName)
    {
        return Path.Combine(RepositoryRoot, "src", functionProjectName, "Program.cs");
    }

    private static IEnumerable<string> EnumerateSourceFiles(params string[] relativeDirectories)
        => relativeDirectories
            .Select(path => Path.Combine(RepositoryRoot, path))
            .Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && File.Exists(Path.Combine(directory.FullName, "AFH.Location.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate AFH.Location solution root.");
    }
}
