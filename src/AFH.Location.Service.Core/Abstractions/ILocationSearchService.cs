using AFH.Location.Service.Core.Contracts.V1.Requests;
using AFH.Location.Service.Core.Contracts.V1.Responses;

namespace AFH.Location.Service.Core.Abstractions;

public interface ILocationSearchService
{
    Task<LocationSearchResponseV1> SearchInPersonAsync(LocationSearchRequestV1 req, CancellationToken ct);
}
