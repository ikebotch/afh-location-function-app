using System;
using System.Net;

namespace AFH.Location.Function.Docs.V1;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
internal sealed class LocationOpenApiOperationAttribute : Attribute
{
    public LocationOpenApiOperationAttribute(string tag, string summary)
    {
        Tag = tag;
        Summary = summary;
    }

    public string Tag { get; }
    public string Summary { get; }
    public string? Description { get; init; }
    public string? HttpMethod { get; init; }
    public Type? RequestBodyType { get; init; }
    public bool RequestBodyRequired { get; init; } = true;
    public Type? ResponseType { get; init; }
    public HttpStatusCode SuccessStatusCode { get; init; } = HttpStatusCode.OK;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
internal sealed class LocationOpenApiTagAttribute : Attribute
{
    public LocationOpenApiTagAttribute(string tag)
    {
        Tag = tag;
    }

    public string Tag { get; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class LocationOpenApiQueryParameterAttribute : Attribute
{
    public LocationOpenApiQueryParameterAttribute(string name, string type)
    {
        Name = name;
        Type = type;
    }

    public string Name { get; }
    public string Type { get; }
    public bool IsRequired { get; init; }
    public string? Description { get; init; }
    public string? Format { get; init; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
internal sealed class LocationOpenApiExcludeAttribute : Attribute;

