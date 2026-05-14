using AFH.Location.Application.Models.V1.Requests;
using AFH.Location.Application.Models.V1.Results;

namespace AFH.Location.Application.Abstractions.Search;

public interface ILocationSearchService
{
    Task<LocationSearchResult> SearchInPersonAsync(LocationSearchRequest req, CancellationToken ct);
}
