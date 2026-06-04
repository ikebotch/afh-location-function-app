using System.Collections.Immutable;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AFH.Common.Errors.AzureFunctions.Builders;
using AFH.Common.Errors.AzureFunctions.DependencyInjection;
using AFH.Common.Errors.Builders;
using AFH.Common.Errors.Models;
using AFH.Location.Domain.Errors;
using AFH.Location.Function.Middleware;
using AFH.Location.Infrastructure.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFH.Location.Tests;

public sealed class LocationExceptionHandlingMiddlewareTests
{
    [Fact]
    public void LocationExceptionMapper_MapsDestinationResolveException()
    {
        var mapping = new LocationExceptionMapper().TryMap(new DestinationResolveException("DESTINATION_INVALID", "Destination is invalid."));

        Assert.Equal((int)HttpStatusCode.UnprocessableEntity, mapping.MappingResult.StatusCode);
        Assert.Equal("DESTINATION_INVALID", mapping.MappingResult.ErrorCode.Value);
        Assert.Equal("DestinationResolution", mapping.FailureSource);
    }

    [Fact]
    public void LocationExceptionMapper_MapsJsonExceptionToBadRequest()
    {
        var mapping = new LocationExceptionMapper().TryMap(new JsonException("Bad JSON"));

        Assert.Equal((int)HttpStatusCode.BadRequest, mapping.MappingResult.StatusCode);
        Assert.Equal("VALIDATION_ERROR", mapping.MappingResult.ErrorCode.Value);
        Assert.Single(mapping.MappingResult.ValidationErrors);
    }

    [Fact]
    public async Task AzureFunctionErrorResponseBuilder_UsesLocationMappingForHandledErrors()
    {
        var services = new ServiceCollection();
        services.AddAfhCommonErrorsAzureFunctions();
        var mapper = new LocationExceptionMapper();
        services.AddSingleton(mapper);
        services.AddSingleton<AFH.Common.Errors.Abstractions.IExceptionMapper>(mapper);

        var request = TestHttpRequestData.Create();
        var mapping = mapper.TryMap(
            new DestinationResolveException("DESTINATION_INVALID", "Destination is invalid."),
            new ErrorContext(
                TraceId: "inv-123",
                CorrelationId: "ctx-correlation",
                Path: request.Url.AbsolutePath,
                Method: request.Method,
                Operation: request.FunctionContext.FunctionDefinition.Name));

        var builder = services.BuildServiceProvider().GetRequiredService<AzureFunctionErrorResponseBuilder>();
        var response = await builder.BuildAsync(request, mapping.MappingResult);
        var payload = await ReadBodyAsync(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("\"code\":\"DESTINATION_INVALID\"", payload);
        Assert.Contains("\"correlationId\":\"ctx-correlation\"", payload);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_ErrorResponse_ReplacesExistingContentTypeHeader()
    {
        var services = new ServiceCollection();
        services.AddAfhCommonErrorsAzureFunctions();
        await using var provider = services.BuildServiceProvider();

        var mapper = new LocationExceptionMapper();
        var mapping = mapper.TryMap(new JsonException("Bad JSON"));
        var request = TestHttpRequestData.Create(prepopulateResponseContentType: true);
        var sut = new ExceptionHandlingMiddleware(
            Options.Create(new ApplicationLoggingOptions()),
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            mapper,
            provider.GetRequiredService<ErrorResponseBuilder>());

        var method = typeof(ExceptionHandlingMiddleware).GetMethod(
            "BuildErrorResponseAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = (Task<HttpResponseData>)method!.Invoke(
            sut,
            [request, mapping.MappingResult, CancellationToken.None])!;
        var response = await task;
        var payload = await ReadBodyAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Content-Type", out var contentTypes));
        Assert.Equal("application/json; charset=utf-8", Assert.Single(contentTypes!));
        Assert.Contains("\"code\":\"VALIDATION_ERROR\"", payload);
    }

    private static async Task<string> ReadBodyAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var payload = await reader.ReadToEndAsync();
        response.Body.Position = 0;
        return payload;
    }
}

internal sealed class TestFunctionContext : FunctionContext
{
    private readonly Dictionary<object, object> _items = [];
    public override string InvocationId => "inv-123";
    public override string FunctionId => "func-123";
    public override TraceContext TraceContext => throw new NotSupportedException();
    public override BindingContext BindingContext => throw new NotSupportedException();
    public override RetryContext RetryContext => null!;
    public override IServiceProvider InstanceServices { get; set; } = new ServiceCollection().BuildServiceProvider();
    public override FunctionDefinition FunctionDefinition { get; } = new TestFunctionDefinition();
    public override IDictionary<object, object> Items
    {
        get => _items;
        set
        {
            _items.Clear();
            foreach (var pair in value)
            {
                _items[pair.Key] = pair.Value;
            }
        }
    }
    public override IInvocationFeatures Features => throw new NotSupportedException();
    public override CancellationToken CancellationToken => CancellationToken.None;
}

internal sealed class TestFunctionDefinition : FunctionDefinition
{
    public override string PathToAssembly => string.Empty;
    public override string EntryPoint => string.Empty;
    public override string Id => "func-123";
    public override string Name => "LocationErrorsFunction";
    public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; } = ImmutableDictionary<string, BindingMetadata>.Empty;
    public override IImmutableDictionary<string, BindingMetadata> OutputBindings { get; } = ImmutableDictionary<string, BindingMetadata>.Empty;
    public override ImmutableArray<FunctionParameter> Parameters { get; } = ImmutableArray<FunctionParameter>.Empty;
}

internal sealed class TestHttpRequestData(FunctionContext functionContext, Uri? url = null, string method = "POST") : HttpRequestData(functionContext)
{
    private readonly bool _prepopulateResponseContentType;

    public TestHttpRequestData(
        FunctionContext functionContext,
        Uri? url = null,
        string method = "POST",
        bool prepopulateResponseContentType = false)
        : this(functionContext, url, method)
    {
        _prepopulateResponseContentType = prepopulateResponseContentType;
    }

    public override Stream Body { get; } = new MemoryStream();
    public override HttpHeadersCollection Headers { get; } = [];
    public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = [];
    public override Uri Url { get; } = url ?? new Uri("https://localhost/api/v1/location/travel-coverage");
    public override IEnumerable<ClaimsIdentity> Identities { get; } = [];
    public override string Method { get; } = method;
    public override HttpResponseData CreateResponse()
    {
        var response = new TestHttpResponseData(FunctionContext);
        if (_prepopulateResponseContentType)
            response.Headers.Add("Content-Type", "text/plain");

        return response;
    }

    public static TestHttpRequestData Create(bool prepopulateResponseContentType = false)
    {
        var context = new TestFunctionContext();
        context.Items[CorrelationIdMiddleware.ItemKey] = "ctx-correlation";
        return new TestHttpRequestData(context, prepopulateResponseContentType: prepopulateResponseContentType);
    }
}

internal sealed class TestHttpCookies : HttpCookies
{
    private readonly List<IHttpCookie> _cookies = [];
    public override void Append(string name, string value) => _cookies.Add(new HttpCookie(name, value));
    public override void Append(IHttpCookie cookie) => _cookies.Add(cookie);
    public override IHttpCookie CreateNew() => new HttpCookie(string.Empty, string.Empty);
}

internal sealed class TestHttpResponseData(FunctionContext functionContext) : HttpResponseData(functionContext)
{
    public override HttpStatusCode StatusCode { get; set; }
    public override HttpHeadersCollection Headers { get; set; } = [];
    public override Stream Body { get; set; } = new MemoryStream();
    public override HttpCookies Cookies { get; } = new TestHttpCookies();
}