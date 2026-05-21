using System;

namespace AFH.Location.Contract.V1.Docs;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class OpenApiExampleAttribute : Attribute
{
    public string Json { get; }
    public OpenApiExampleAttribute(string json)
    {
        Json = json;
    }
}
