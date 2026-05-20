using AFH.Location.Application.Models.V1.Travel;

namespace AFH.Location.Application.Abstractions.Travel;

public interface IPostcodeCoordinateResolver
{
    Task<PostcodeCoordinateResolution> ResolveAsync(string postcode, CancellationToken ct);
}
