using AFH.Location.Service.Api.Contracts;
using AFH.Location.Service.Core.Abstractions;
using AFH.Location.Service.Core.Contracts.V1.Requests;
using AFH.Location.Service.Core.Contracts.V1.Responses;
using AFH.Location.Service.Core.Validation.V1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace AFH.Location.Service.Api.Functions.V1;

public sealed class LocationSearchBatchFunctionV1
{
    private readonly ILocationSearchService _service;
    private readonly int _maxParallel;

    public LocationSearchBatchFunctionV1(
        ILocationSearchService service,
        IConfiguration configuration)
    {
        _service = service;
        _maxParallel = Math.Clamp(configuration.GetValue<int?>("LocationSearch:Batch:MaxParallel") ?? 6, 1, 16);
    }

    [Function("LocationSearchBatchV1")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/location/inperson/advisers/search/batch")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var payload = await req.ReadFromJsonAsync<LocationSearchBatchRequestV1>(cancellationToken: ct);
        if (payload is null || payload.Requests.Count == 0)
        {
            return await req.WriteFailureAsync(
                HttpStatusCode.BadRequest,
                new { code = "VALIDATION_ERROR", message = "Requests payload is required and cannot be empty." },
                ct);
        }

        var indexed = payload.Requests
            .Select((request, index) => (request, index))
            .ToArray();

        var results = new LocationSearchBatchItemResponseV1[indexed.Length];
        using var gate = new SemaphoreSlim(_maxParallel, _maxParallel);

        var tasks = indexed.Select(async item =>
        {
            await gate.WaitAsync(ct);
            try
            {
                if (item.request is null)
                {
                    results[item.index] = new LocationSearchBatchItemResponseV1
                    {
                        RequestId = string.Empty,
                        Success = false,
                        ErrorCode = "VALIDATION_ERROR",
                        ErrorMessage = "Request item cannot be null."
                    };
                    return;
                }

                var validationErrors = LocationSearchRequestValidatorV1.Validate(item.request);
                if (validationErrors.Count > 0)
                {
                    results[item.index] = new LocationSearchBatchItemResponseV1
                    {
                        RequestId = item.request.RequestId ?? string.Empty,
                        Success = false,
                        ErrorCode = "VALIDATION_ERROR",
                        ErrorMessage = string.Join("; ", validationErrors)
                    };
                    return;
                }

                var search = await _service.SearchInPersonAsync(item.request, ct);
                results[item.index] = new LocationSearchBatchItemResponseV1
                {
                    RequestId = item.request.RequestId ?? string.Empty,
                    Success = true,
                    Result = search
                };
            }
            catch (Exception ex)
            {
                results[item.index] = new LocationSearchBatchItemResponseV1
                {
                    RequestId = item.request?.RequestId ?? string.Empty,
                    Success = false,
                    ErrorCode = "SEARCH_ERROR",
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);

        var response = new LocationSearchBatchResponseV1
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Results = results.ToList()
        };

        var paging = ApiEnvelopeExtensions.SinglePage(response.Results.Count);
        return await req.WriteSuccessAsync(response, ct, paging);
    }
}
