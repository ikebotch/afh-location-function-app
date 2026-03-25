using AFH.Location.Service.Application.Models.V1;

namespace AFH.Location.Service.Application.Abstractions;

public interface ILocationSearchService
{
    Task<LocationSearchResult> SearchInPersonAsync(LocationSearchRequest req, CancellationToken ct);
}
