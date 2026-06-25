using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AFH.Location.Tests;

internal static class TestConfiguration
{
    public static IConfiguration Create(IReadOnlyDictionary<string, string?> values)
    {
        var path = Path.Combine(Path.GetTempPath(), $"afh-location-config-{Guid.NewGuid():N}.json");
        var nested = BuildNestedValues(values);
        File.WriteAllText(path, JsonSerializer.Serialize(nested));

        return new ConfigurationBuilder()
            .AddJsonFile(path, optional: false, reloadOnChange: false)
            .Build();
    }

    private static Dictionary<string, object?> BuildNestedValues(IReadOnlyDictionary<string, string?> values)
    {
        var root = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in values)
        {
            var target = root;
            var segments = item.Key.Split(':', StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < segments.Length; index++)
            {
                var segment = segments[index];
                if (index == segments.Length - 1)
                {
                    target[segment] = item.Value;
                    continue;
                }

                if (target.TryGetValue(segment, out var existing) && existing is Dictionary<string, object?> child)
                {
                    target = child;
                    continue;
                }

                child = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                target[segment] = child;
                target = child;
            }
        }

        return root;
    }
}
