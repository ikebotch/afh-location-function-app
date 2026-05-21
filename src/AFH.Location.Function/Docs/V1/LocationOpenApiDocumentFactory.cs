using AFH.Location.Function.Functions.Common;
using Microsoft.Azure.Functions.Worker;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AFH.Location.Contract.V1.Docs;

namespace AFH.Location.Function.Docs.V1;

internal static class LocationOpenApiDocumentFactory
{
    private static readonly Type ProblemResponseType = typeof(ApiEnvelope<>).MakeGenericType(typeof(LocationErrorResponse));
    private static readonly NullabilityInfoContext NullabilityContext = new();

    public static string CreateOpenApiJson(Uri requestUrl)
    {
        var baseUrl = $"{requestUrl.Scheme}://{requestUrl.Host}";
        if (!requestUrl.IsDefaultPort)
            baseUrl += $":{requestUrl.Port}";

        var schemaTypes = new HashSet<Type> { ProblemResponseType, typeof(LocationErrorResponse) };
        var paths = BuildPaths(schemaTypes);

        var doc = new Dictionary<string, object>
        {
            ["openapi"] = "3.0.1",
            ["info"] = new Dictionary<string, object>
            {
                ["title"] = "AFH Location Service API",
                ["version"] = "v1",
                ["description"] = "Location adviser discovery and ranking API."
            },
            ["servers"] = new object[]
            {
                new Dictionary<string, object> { ["url"] = $"{baseUrl}/api" }
            },
            ["paths"] = paths,
            ["components"] = new Dictionary<string, object>
            {
                ["schemas"] = schemaTypes
                    .Distinct()
                    .OrderBy(GetSchemaName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => GetSchemaName(x), x => (object)FromType(x), StringComparer.OrdinalIgnoreCase)
            }
        };

        return JsonSerializer.Serialize(doc, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        });
    }

    private static Dictionary<string, object> BuildPaths(ISet<Type> schemaTypes)
    {
        var paths = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in typeof(LocationOpenApiDocumentFactory).Assembly.GetTypes().OrderBy(x => x.FullName, StringComparer.Ordinal))
        {
            if (type.GetCustomAttribute<LocationOpenApiExcludeAttribute>(inherit: false) is not null)
                continue;

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.GetCustomAttribute<FunctionAttribute>(inherit: false) is null)
                    continue;

                if (method.GetCustomAttribute<LocationOpenApiExcludeAttribute>(inherit: false) is not null)
                    continue;

                var httpTrigger = method.GetParameters()
                    .SelectMany(parameter => parameter.GetCustomAttributes<Microsoft.Azure.Functions.Worker.HttpTriggerAttribute>(inherit: false))
                    .SingleOrDefault();

                if (httpTrigger is null)
                    continue;

                var route = "/" + (httpTrigger.Route?.TrimStart('/') ?? string.Empty);
                if (!paths.TryGetValue(route, out var pathItemObj))
                {
                    pathItemObj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    paths[route] = pathItemObj;
                }

                var pathItem = (Dictionary<string, object>)pathItemObj;
                var metadata = ResolveOperationMetadata(type, method);

                foreach (var httpMethod in httpTrigger.Methods.Select(x => x.ToLowerInvariant()))
                {
                    var operation = ResolveMetadataForHttpMethod(metadata, httpMethod);
                    var parameters = BuildParameters(route, method);

                    if (operation.RequestBodyType is not null)
                        schemaTypes.Add(operation.RequestBodyType);

                    if (operation.ResponseType is not null)
                        schemaTypes.Add(GetSuccessEnvelopeType(operation.ResponseType));

                    pathItem[httpMethod] = BuildOperation(httpMethod, operation, parameters);
                }
            }
        }

        return paths;
    }

    private static Dictionary<string, object> BuildOperation(
        string httpMethod,
        LocationOpenApiOperationAttribute operation,
        IReadOnlyList<object> parameters)
    {
        var value = new Dictionary<string, object>
        {
            ["tags"] = new[] { operation.Tag },
            ["summary"] = operation.Summary,
            ["responses"] = BuildResponses(operation)
        };

        if (!string.IsNullOrWhiteSpace(operation.Description))
            value["description"] = operation.Description;

        if (parameters.Count > 0)
            value["parameters"] = parameters;

        if (operation.RequestBodyType is not null && httpMethod is "post" or "put" or "patch")
        {
            value["requestBody"] = new Dictionary<string, object>
            {
                ["required"] = operation.RequestBodyRequired,
                ["content"] = new Dictionary<string, object>
                {
                    ["application/json"] = new Dictionary<string, object>
                    {
                        ["schema"] = SchemaRef(operation.RequestBodyType)
                    }
                }
            };
        }

        return value;
    }

    private static Dictionary<string, object> BuildResponses(LocationOpenApiOperationAttribute operation)
    {
        var responses = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [((int)operation.SuccessStatusCode).ToString()] = operation.ResponseType is null
                ? new Dictionary<string, object> { ["description"] = "Success" }
                : new Dictionary<string, object>
                {
                    ["description"] = "Success",
                    ["content"] = new Dictionary<string, object>
                    {
                        ["application/json"] = new Dictionary<string, object>
                        {
                            ["schema"] = SchemaRef(GetSuccessEnvelopeType(operation.ResponseType))
                        }
                    }
                }
        };

        responses["400"] = ProblemResponse("Validation or domain error");
        responses["401"] = ProblemResponse("Unauthorized");
        responses["403"] = ProblemResponse("Forbidden");
        responses["404"] = ProblemResponse("Not Found");
        responses["500"] = ProblemResponse("Server Error");
        return responses;
    }

    private static Dictionary<string, object> ProblemResponse(string description)
        => new()
        {
            ["description"] = description,
            ["content"] = new Dictionary<string, object>
            {
                ["application/json"] = new Dictionary<string, object>
                {
                    ["schema"] = SchemaRef(ProblemResponseType)
                }
            }
        };

    private static IReadOnlyList<object> BuildParameters(string route, MethodInfo method)
    {
        var parameters = route.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment.StartsWith('{') && segment.EndsWith('}'))
            .Select(segment => (object)new Dictionary<string, object>
            {
                ["name"] = segment[1..^1],
                ["in"] = "path",
                ["required"] = true,
                ["schema"] = new Dictionary<string, object> { ["type"] = "string" }
            })
            .ToList();

        foreach (var query in method.GetCustomAttributes<LocationOpenApiQueryParameterAttribute>(inherit: false))
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = query.Type
            };

            if (!string.IsNullOrWhiteSpace(query.Format))
                schema["format"] = query.Format!;

            var parameter = new Dictionary<string, object>
            {
                ["name"] = query.Name,
                ["in"] = "query",
                ["required"] = query.IsRequired,
                ["schema"] = schema
            };

            if (!string.IsNullOrWhiteSpace(query.Description))
                parameter["description"] = query.Description!;

            parameters.Add(parameter);
        }

        return parameters;
    }

    private static LocationOpenApiOperationAttribute ResolveMetadataForHttpMethod(
        IReadOnlyList<LocationOpenApiOperationAttribute> metadata,
        string httpMethod)
        => metadata.FirstOrDefault(x => string.Equals(x.HttpMethod, httpMethod, StringComparison.OrdinalIgnoreCase))
            ?? metadata.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.HttpMethod))
            ?? new LocationOpenApiOperationAttribute(DeriveTag(string.Empty, httpMethod), Humanize(httpMethod));

    private static IReadOnlyList<LocationOpenApiOperationAttribute> ResolveOperationMetadata(Type type, MethodInfo method)
    {
        var methodMetadata = method.GetCustomAttributes<LocationOpenApiOperationAttribute>(inherit: false).ToArray();
        if (methodMetadata.Length > 0)
            return methodMetadata;

        var typeMetadata = type.GetCustomAttributes<LocationOpenApiOperationAttribute>(inherit: false).ToArray();
        if (typeMetadata.Length > 0)
            return typeMetadata;

        var explicitTag = method.GetCustomAttribute<LocationOpenApiTagAttribute>(inherit: false)?.Tag
            ?? type.GetCustomAttribute<LocationOpenApiTagAttribute>(inherit: false)?.Tag;
        var inferredTag = explicitTag ?? DeriveTag(type.Namespace ?? string.Empty, type.Name);
        var inferredSummary = Humanize(type.Name.Replace("Function", string.Empty, StringComparison.OrdinalIgnoreCase));
        return [new LocationOpenApiOperationAttribute(inferredTag, inferredSummary)];
    }

    private static string DeriveTag(string source, string fallback)
    {
        if (source.Contains(".Admin", StringComparison.OrdinalIgnoreCase))
            return "Admin";

        if (fallback.Contains("Health", StringComparison.OrdinalIgnoreCase))
            return "Health";

        return "Location";
    }

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Operation";

        var buffer = new List<char>(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (i > 0 && char.IsUpper(current) && !char.IsUpper(value[i - 1]))
                buffer.Add(' ');

            buffer.Add(current);
        }

        return new string(buffer.ToArray()).Trim();
    }

    private static Type GetSuccessEnvelopeType(Type responseType)
        => typeof(ApiEnvelope<>).MakeGenericType(responseType);

    private static string GetSchemaName(Type type)
    {
        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;

        if (!unwrapped.IsGenericType)
        {
            var schemaName = unwrapped.Name;
            if (unwrapped.Namespace?.Contains(".Contracts.V2.", StringComparison.OrdinalIgnoreCase) == true)
                schemaName += "V2";

            return schemaName;
        }

        var genericName = unwrapped.Name[..unwrapped.Name.IndexOf('`')];
        var argumentNames = string.Join("And", unwrapped.GetGenericArguments().Select(GetSchemaName));
        return $"{genericName}Of{argumentNames}";
    }

    private static Dictionary<string, object> SchemaRef(Type dtoType)
        => new() { ["$ref"] = $"#/components/schemas/{GetSchemaName(dtoType)}" };

    private static Dictionary<string, object> FromType(Type type)
    {
        var schema = BuildSchema(type);
        var exampleAttr = type.GetCustomAttribute<OpenApiExampleAttribute>();
        
        if (exampleAttr != null)
        {
            try
            {
                schema["example"] = JsonNode.Parse(exampleAttr.Json)!;
            }
            catch
            {
                // Ignore parse errors, scalar will just display string
            }
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApiEnvelope<>))
        {
            var innerType = type.GetGenericArguments()[0];
            var innerExampleAttr = innerType.GetCustomAttribute<OpenApiExampleAttribute>();
            
            if (innerExampleAttr != null)
            {
                try
                {
                    var innerNode = JsonNode.Parse(innerExampleAttr.Json);
                    var envelopeNode = new JsonObject
                    {
                        ["success"] = true,
                        ["data"] = innerNode,
                        ["paging"] = null
                    };
                    schema["example"] = envelopeNode;
                }
                catch { }
            }
            else if (innerType == typeof(LocationErrorResponse))
            {
                var envelopeNode = new JsonObject
                {
                    ["success"] = false,
                    ["data"] = new JsonObject
                    {
                        ["code"] = "VALIDATION_ERROR",
                        ["message"] = "Invalid request.",
                        ["errors"] = new JsonArray("sourcePostcode is required.")
                    }
                };
                schema["example"] = envelopeNode;
            }
        }

        return schema;
    }

    private static Dictionary<string, object> BuildSchema(Type type)
    {
        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;

        if (unwrapped == typeof(string))
            return new Dictionary<string, object> { ["type"] = "string" };
        if (unwrapped == typeof(bool))
            return new Dictionary<string, object> { ["type"] = "boolean" };
        if (unwrapped == typeof(int) || unwrapped == typeof(long) || unwrapped == typeof(short))
            return new Dictionary<string, object> { ["type"] = "integer" };
        if (unwrapped == typeof(float) || unwrapped == typeof(double) || unwrapped == typeof(decimal))
            return new Dictionary<string, object> { ["type"] = "number" };
        if (unwrapped == typeof(DateTime) || unwrapped == typeof(DateTimeOffset))
            return new Dictionary<string, object> { ["type"] = "string", ["format"] = "date-time" };
        if (unwrapped == typeof(DateOnly))
            return new Dictionary<string, object> { ["type"] = "string", ["format"] = "date" };
        if (unwrapped == typeof(TimeSpan))
            return new Dictionary<string, object> { ["type"] = "string", ["format"] = "duration" };
        if (unwrapped == typeof(Guid))
            return new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" };
        if (unwrapped == typeof(object))
            return new Dictionary<string, object> { ["type"] = "object" };

        if (unwrapped.IsEnum)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = Enum.GetNames(unwrapped)
            };
        }

        if (TryGetDictionaryValueType(unwrapped, out var valueType))
        {
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["additionalProperties"] = BuildSchema(valueType)
            };
        }

        if (TryGetEnumerableElementType(unwrapped, out var elementType))
        {
            return new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = BuildSchema(elementType)
            };
        }

        var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var required = new List<string>();

        var props = unwrapped
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.GetMethod is not null && p.GetMethod.IsPublic && p.GetIndexParameters().Length == 0);

        foreach (var prop in props)
        {
            var propertyName = ResolvePropertyName(prop);
            properties[propertyName] = BuildSchema(prop.PropertyType);

            if (IsRequired(prop))
                required.Add(propertyName);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
            schema["required"] = required;

        return schema;
    }

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type == typeof(string))
        {
            elementType = typeof(string);
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        var enumerableType = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableType is not null)
        {
            elementType = enumerableType.GetGenericArguments()[0];
            return true;
        }

        elementType = typeof(object);
        return false;
    }

    private static bool TryGetDictionaryValueType(Type type, out Type valueType)
    {
        var dictionaryType = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>)
            ? type
            : type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        if (dictionaryType is not null && dictionaryType.GetGenericArguments()[0] == typeof(string))
        {
            valueType = dictionaryType.GetGenericArguments()[1];
            return true;
        }

        valueType = typeof(object);
        return false;
    }

    private static string ResolvePropertyName(PropertyInfo property)
    {
        var jsonName = property.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name;
        if (!string.IsNullOrWhiteSpace(jsonName))
            return jsonName;

        return char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
    }

    private static bool IsRequired(PropertyInfo property)
    {
        var propertyType = property.PropertyType;
        if (Nullable.GetUnderlyingType(propertyType) is not null)
            return false;

        if (propertyType.IsValueType)
            return true;

        var nullability = NullabilityContext.Create(property);
        return nullability.WriteState == NullabilityState.NotNull || nullability.ReadState == NullabilityState.NotNull;
    }
}
