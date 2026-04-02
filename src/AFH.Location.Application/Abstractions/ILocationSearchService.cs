using AFH.Location.Application.Models.V1;

namespace AFH.Location.Application.Abstractions;

public interface ILocationSearchService
{
    Task<LocationSearchResult> SearchInPersonAsync(LocationSearchRequest req, CancellationToken ct);
}
