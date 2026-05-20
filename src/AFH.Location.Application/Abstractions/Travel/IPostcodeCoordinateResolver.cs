using AFH.Location.Application.Models.V1.Travel;

namespace AFH.Location.Application.Abstractions.Travel;

public interface IPostcodeCoordinateResolver
{
    Task<PostcodeCoordinateResolution> ResolveAsync(string postcode, CancellationToken ct);

    async Task<IReadOnlyDictionary<string, PostcodeCoordinateResolution>> ResolveManyAsync(
        IReadOnlyDictionary<string, string> postcodesByKey,
        CancellationToken ct)
    {
        var results = new Dictionary<string, PostcodeCoordinateResolution>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in postcodesByKey)
            results[item.Key] = await ResolveAsync(item.Value, ct);

        return results;
    }
}
