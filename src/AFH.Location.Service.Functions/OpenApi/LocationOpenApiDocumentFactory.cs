using AFH.Location.Service.Api.Contracts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AFH.Location.Service.Api.OpenApi;

internal static class LocationOpenApiDocumentFactory
{
    private static readonly JsonNamingPolicy NamingPolicy = JsonNamingPolicy.CamelCase;
    private static readonly NullabilityInfoContext NullabilityInfoContext = new();
    private static readonly Regex RouteParameterPattern = new("{([^}]+)}", RegexOptions.Compiled);

    public static readonly string OpenApiJson = CreateOpenApiJson();

    private static string CreateOpenApiJson()
    {
        var components = new JsonObject();
        var processing = new HashSet<string>(StringComparer.Ordinal);
        var paths = BuildPaths(components, processing);

        var document = new JsonObject
        {
            ["openapi"] = "3.0.3",
            ["info"] = new JsonObject
            {
                ["title"] = "AFH Location Service API",
                ["version"] = "v1",
                ["description"] = "Location adviser discovery, search, coverage, and internal administration API."
            },
            ["servers"] = new JsonArray
            {
                new JsonObject { ["url"] = "/api" }
            },
            ["paths"] = paths,
            ["components"] = new JsonObject
            {
                ["schemas"] = components
            }
        };

        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonObject BuildPaths(JsonObject components, HashSet<string> processing)
    {
        var paths = new JsonObject();
        var assembly = typeof(LocationOpenApiDocumentFactory).Assembly;

        var methods = assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttribute<FunctionAttribute>() is not null)
            .Where(m => m.GetCustomAttribute<LocationOpenApiExcludeAttribute>() is null);

        foreach (var method in methods)
        {
            var operationMetadata = method.GetCustomAttribute<LocationOpenApiOperationAttribute>();
            if (operationMetadata is null)
                continue;

            var triggerParameter = method.GetParameters()
                .FirstOrDefault(p => p.GetCustomAttribute<HttpTriggerAttribute>() is not null);

            var trigger = triggerParameter?.GetCustomAttribute<HttpTriggerAttribute>();
            if (trigger is null || string.IsNullOrWhiteSpace(trigger.Route))
                continue;

            var path = $"/{trigger.Route.TrimStart('/')}";
            if (paths[path] is not JsonObject pathItem)
            {
                pathItem = new JsonObject();
                paths[path] = pathItem;
            }

            foreach (var verb in trigger.Methods ?? Array.Empty<string>())
            {
                var operation = new JsonObject
                {
                    ["tags"] = new JsonArray(operationMetadata.Tag),
                    ["summary"] = operationMetadata.Summary
                };

                if (!string.IsNullOrWhiteSpace(operationMetadata.Description))
                    operation["description"] = operationMetadata.Description;

                var parameters = BuildParameters(method, trigger.Route);
                if (parameters.Count > 0)
                    operation["parameters"] = parameters;

                if (operationMetadata.RequestBodyType is not null)
                {
                    RegisterType(operationMetadata.RequestBodyType, components, processing);
                    operation["requestBody"] = BuildRequestBody(operationMetadata.RequestBodyType);
                }

                var responses = new JsonObject
                {
                    [operationMetadata.SuccessStatusCode.ToString()] = BuildJsonResponse(
                        operationMetadata.SuccessDescription,
                        operationMetadata.SuccessResponseType,
                        components,
                        processing)
                };

                foreach (var responseMetadata in method.GetCustomAttributes<LocationOpenApiResponseAttribute>())
                {
                    responses[responseMetadata.StatusCode.ToString()] = BuildJsonResponse(
                        responseMetadata.Description,
                        responseMetadata.ResponseType,
                        components,
                        processing);
                }

                operation["responses"] = responses;
                pathItem[verb.ToLowerInvariant()] = operation;
            }
        }

        return paths;
    }

    private static JsonArray BuildParameters(MethodInfo method, string route)
    {
        var parameters = new JsonArray();

        foreach (Match match in RouteParameterPattern.Matches(route))
        {
            var name = match.Groups[1].Value;
            parameters.Add(new JsonObject
            {
                ["name"] = name,
                ["in"] = "path",
                ["required"] = true,
                ["schema"] = new JsonObject
                {
                    ["type"] = "string"
                }
            });
        }

        foreach (var query in method.GetCustomAttributes<LocationOpenApiQueryParameterAttribute>())
        {
            var schema = new JsonObject
            {
                ["type"] = query.Type
            };

            if (!string.IsNullOrWhiteSpace(query.Format))
                schema["format"] = query.Format;

            var parameter = new JsonObject
            {
                ["name"] = query.Name,
                ["in"] = "query",
                ["required"] = query.IsRequired,
                ["schema"] = schema
            };

            if (!string.IsNullOrWhiteSpace(query.Description))
                parameter["description"] = query.Description;

            parameters.Add(parameter);
        }

        return parameters;
    }

    private static JsonObject BuildRequestBody(Type requestType)
    {
        return new JsonObject
        {
            ["required"] = true,
            ["content"] = new JsonObject
            {
                ["application/json"] = new JsonObject
                {
                    ["schema"] = BuildSchemaRef(requestType)
                }
            }
        };
    }

    private static JsonObject BuildJsonResponse(
        string description,
        Type? responseType,
        JsonObject components,
        HashSet<string> processing)
    {
        var response = new JsonObject
        {
            ["description"] = description
        };

        if (responseType is null)
            return response;

        RegisterType(responseType, components, processing);
        response["content"] = new JsonObject
        {
            ["application/json"] = new JsonObject
            {
                ["schema"] = BuildSchemaRef(responseType)
            }
        };

        return response;
    }

    private static void RegisterType(Type type, JsonObject components, HashSet<string> processing)
    {
        if (TryGetPrimitiveSchema(type, out _) || type == typeof(void))
            return;

        var schemaName = GetSchemaName(type);
        if (components.ContainsKey(schemaName) || !processing.Add(schemaName))
            return;

        if (TryGetEnumerableElementType(type, out var elementType))
        {
            components[schemaName] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = BuildTypeSchema(elementType, components, processing)
            };
            processing.Remove(schemaName);
            return;
        }

        var properties = new JsonObject();
        var required = new JsonArray();

        components[schemaName] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetMethod is null || property.GetMethod.GetParameters().Length > 0)
                continue;

            var propertyName = NamingPolicy.ConvertName(property.Name);
            var isNullable = IsNullable(property);
            properties[propertyName] = BuildPropertySchema(property.PropertyType, components, processing, isNullable);

            if (!isNullable)
                required.Add(propertyName);
        }

        if (required.Count > 0)
            ((JsonObject)components[schemaName]!).Add("required", required);

        processing.Remove(schemaName);
    }

    private static JsonObject BuildPropertySchema(Type type, JsonObject components, HashSet<string> processing, bool isNullable)
    {
        var schema = BuildTypeSchema(type, components, processing);
        if (isNullable && !schema.ContainsKey("$ref"))
            schema["nullable"] = true;

        return schema;
    }

    private static JsonObject BuildTypeSchema(Type type, JsonObject components, HashSet<string> processing)
    {
        if (TryGetPrimitiveSchema(type, out var primitive))
            return primitive;

        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
            return BuildTypeSchema(nullable, components, processing);

        if (TryGetEnumerableElementType(type, out var elementType))
        {
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = BuildTypeSchema(elementType, components, processing)
            };
        }

        RegisterType(type, components, processing);
        return BuildSchemaRef(type);
    }

    private static JsonObject BuildSchemaRef(Type type)
        => new() { ["$ref"] = $"#/components/schemas/{GetSchemaName(type)}" };

    private static string GetSchemaName(Type type)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
            return GetSchemaName(nullable);

        if (!type.IsGenericType)
            return type.Name;

        var genericName = type.Name[..type.Name.IndexOf('`')];
        var arguments = string.Join("And", type.GetGenericArguments().Select(GetSchemaName));
        return $"{genericName}Of{arguments}";
    }

    private static bool TryGetPrimitiveSchema(Type type, out JsonObject schema)
    {
        var normalized = Nullable.GetUnderlyingType(type) ?? type;

        if (normalized == typeof(string) || normalized == typeof(Guid))
        {
            schema = new JsonObject { ["type"] = "string" };
            return true;
        }

        if (normalized == typeof(DateTime) || normalized == typeof(DateTimeOffset))
        {
            schema = new JsonObject { ["type"] = "string", ["format"] = "date-time" };
            return true;
        }

        if (normalized == typeof(bool))
        {
            schema = new JsonObject { ["type"] = "boolean" };
            return true;
        }

        if (normalized.IsEnum)
        {
            schema = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray(Enum.GetNames(normalized).Select(x => (JsonNode?)x).ToArray())
            };
            return true;
        }

        if (normalized == typeof(int) || normalized == typeof(long) || normalized == typeof(short))
        {
            schema = new JsonObject { ["type"] = "integer" };
            return true;
        }

        if (normalized == typeof(float) || normalized == typeof(double) || normalized == typeof(decimal))
        {
            schema = new JsonObject { ["type"] = "number" };
            return true;
        }

        schema = null!;
        return false;
    }

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type == typeof(string))
        {
            elementType = null!;
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        var enumerableInterface = type.GetInterfaces()
            .Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableInterface is null)
        {
            elementType = null!;
            return false;
        }

        elementType = enumerableInterface.GetGenericArguments()[0];
        return true;
    }

    private static bool IsNullable(PropertyInfo property)
    {
        if (!property.PropertyType.IsValueType)
        {
            var nullability = NullabilityInfoContext.Create(property);
            return nullability.ReadState != NullabilityState.NotNull;
        }

        return Nullable.GetUnderlyingType(property.PropertyType) is not null;
    }
}
