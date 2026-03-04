using AFH.Location.Service.Core.Contracts.V1.Requests;
using AFH.Location.Service.Core.Contracts.V1.Responses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace AFH.Location.Service.Api.Functions.V1;

public sealed class ApiDocsFunctions
{
    [Function("OpenApiV1")]
    public async Task<HttpResponseData> OpenApi(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "openapi/v1.json")] HttpRequestData req)
    {
        var baseUrl = $"{req.Url.Scheme}://{req.Url.Host}";
        if (!req.Url.IsDefaultPort) baseUrl += $":{req.Url.Port}";

        var components = new Dictionary<string, object>(StringComparer.Ordinal);
        var locationSearchRequestSchema = SchemaRefOrInline(typeof(LocationSearchRequestV1), components);
        var locationSearchResponseSchema = SchemaRefOrInline(typeof(LocationSearchResponseV1), components);
        var licenseListResponseSchema = SchemaRefOrInline(typeof(LicenseListResponseV1), components);

        var doc = new Dictionary<string, object>
        {
            ["openapi"] = "3.0.1",
            ["info"] = new Dictionary<string, object>
            {
                ["title"] = "AFH Location Service API",
                ["version"] = "v1",
                ["description"] = "Location adviser discovery and ranking API."
            },
            ["servers"] = new[]
            {
                new Dictionary<string, object> { ["url"] = $"{baseUrl}/api" }
            },
            ["paths"] = new Dictionary<string, object>
            {
                ["/v1/location/health"] = new Dictionary<string, object>
                {
                    ["get"] = new Dictionary<string, object>
                    {
                        ["tags"] = new[] { "Health" },
                        ["summary"] = "Health check",
                        ["responses"] = new Dictionary<string, object>
                        {
                            ["200"] = new Dictionary<string, object> { ["description"] = "Service healthy" }
                        }
                    }
                },
                ["/v1/location/licenses"] = new Dictionary<string, object>
                {
                    ["get"] = new Dictionary<string, object>
                    {
                        ["tags"] = new[] { "License" },
                        ["summary"] = "Get active adviser licenses",
                        ["responses"] = new Dictionary<string, object>
                        {
                            ["200"] = new Dictionary<string, object>
                            {
                                ["description"] = "License list",
                                ["content"] = new Dictionary<string, object>
                                {
                                    ["application/json"] = new Dictionary<string, object>
                                    {
                                        ["schema"] = licenseListResponseSchema
                                    }
                                }
                            }
                        }
                    }
                },
                ["/v1/location/inperson/advisers/search"] = new Dictionary<string, object>
                {
                    ["post"] = new Dictionary<string, object>
                    {
                        ["tags"] = new[] { "LocationSearch" },
                        ["summary"] = "Search in-person advisers",
                        ["requestBody"] = new Dictionary<string, object>
                        {
                            ["required"] = true,
                            ["content"] = new Dictionary<string, object>
                            {
                                ["application/json"] = new Dictionary<string, object>
                                {
                                    ["schema"] = locationSearchRequestSchema
                                }
                            }
                        },
                        ["responses"] = new Dictionary<string, object>
                        {
                            ["200"] = new Dictionary<string, object>
                            {
                                ["description"] = "Ranked adviser candidates",
                                ["content"] = new Dictionary<string, object>
                                {
                                    ["application/json"] = new Dictionary<string, object>
                                    {
                                        ["schema"] = locationSearchResponseSchema
                                    }
                                }
                            },
                            ["400"] = new Dictionary<string, object> { ["description"] = "Validation error" },
                            ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized" },
                            ["500"] = new Dictionary<string, object> { ["description"] = "Server error" }
                        }
                    }
                },
                ["/v2/location/inperson/advisers/search"] = new Dictionary<string, object>
                {
                    ["post"] = new Dictionary<string, object>
                    {
                        ["tags"] = new[] { "LocationSearchV2" },
                        ["summary"] = "Search in-person advisers (v2)",
                        ["responses"] = new Dictionary<string, object>
                        {
                            ["200"] = new Dictionary<string, object> { ["description"] = "Success" }
                        }
                    }
                }
            },
            ["components"] = new Dictionary<string, object>
            {
                ["schemas"] = components
            }
        };

        var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        });

        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await res.WriteStringAsync(json);
        return res;
    }

    [Function("ScalarUi")]
    public async Task<HttpResponseData> Scalar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scalar")] HttpRequestData req)
    {
        var html = """
                   <!doctype html>
                   <html lang="en">
                   <head>
                     <meta charset="utf-8" />
                     <meta name="viewport" content="width=device-width,initial-scale=1" />
                     <title>AFH Location Service Docs</title>
                   </head>
                   <body>
                     <script id="api-reference" data-url="/api/openapi/v1.json"></script>
                     <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
                   </body>
                   </html>
                   """;

        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "text/html; charset=utf-8");
        await res.WriteStringAsync(html);
        return res;
    }

    private static object SchemaRefOrInline(Type type, IDictionary<string, object> components)
    {
        var nullableUnderlying = Nullable.GetUnderlyingType(type);
        if (nullableUnderlying is not null)
            type = nullableUnderlying;

        if (IsPrimitiveOpenApiType(type, out var primitiveSchema))
            return primitiveSchema;

        if (TryGetCollectionElementType(type, out var elementType))
        {
            return new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = SchemaRefOrInline(elementType, components)
            };
        }

        var schemaName = type.Name;
        if (!components.ContainsKey(schemaName))
        {
            components[schemaName] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>(),
                ["required"] = new List<string>()
            };

            var schemaObj = (Dictionary<string, object>)components[schemaName];
            var props = (Dictionary<string, object>)schemaObj["properties"];
            var required = (List<string>)schemaObj["required"];

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                props[ToCamelCase(prop.Name)] = SchemaRefOrInline(prop.PropertyType, components);

                if (IsRequiredProperty(prop.PropertyType))
                    required.Add(ToCamelCase(prop.Name));
            }

            if (required.Count == 0)
                schemaObj.Remove("required");
        }

        return new Dictionary<string, object> { ["$ref"] = $"#/components/schemas/{schemaName}" };
    }

    private static bool IsRequiredProperty(Type type)
    {
        if (!type.IsValueType)
            return false;

        return Nullable.GetUnderlyingType(type) is null;
    }

    private static bool IsPrimitiveOpenApiType(Type type, out object schema)
    {
        schema = new Dictionary<string, object>();

        if (type == typeof(string))
        {
            schema = new Dictionary<string, object> { ["type"] = "string" };
            return true;
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            schema = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["format"] = "date-time"
            };
            return true;
        }

        if (type == typeof(bool))
        {
            schema = new Dictionary<string, object> { ["type"] = "boolean" };
            return true;
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
        {
            schema = new Dictionary<string, object> { ["type"] = "integer" };
            return true;
        }

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            schema = new Dictionary<string, object> { ["type"] = "number" };
            return true;
        }

        if (type.IsEnum)
        {
            schema = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = Enum.GetNames(type)
            };
            return true;
        }

        return false;
    }

    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        var enumerableType = type
            .GetInterfaces()
            .Concat(new[] { type })
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableType is not null)
        {
            elementType = enumerableType.GetGenericArguments()[0];
            return true;
        }

        elementType = typeof(object);
        return false;
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
            return value;

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
