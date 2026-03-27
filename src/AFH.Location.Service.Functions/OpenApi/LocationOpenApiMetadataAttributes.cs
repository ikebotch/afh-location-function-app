namespace AFH.Location.Service.Api.OpenApi;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
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
    public Type? RequestBodyType { get; init; }
    public Type? SuccessResponseType { get; init; }
    public int SuccessStatusCode { get; init; } = 200;
    public string SuccessDescription { get; init; } = "Success";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class LocationOpenApiResponseAttribute : Attribute
{
    public LocationOpenApiResponseAttribute(int statusCode, string description)
    {
        StatusCode = statusCode;
        Description = description;
    }

    public int StatusCode { get; }
    public string Description { get; }
    public Type? ResponseType { get; init; }
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

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class LocationOpenApiExcludeAttribute : Attribute;
