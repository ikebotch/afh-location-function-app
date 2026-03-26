using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Location.Service.Api.Contracts;

public static class ApiEnvelopeExtensions
{
    public static async Task<HttpResponseData> WriteSuccessAsync<T>(
        this HttpRequestData req,
        T data,
        CancellationToken ct,
        ApiPaging? paging = null,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var res = req.CreateResponse(statusCode);
        await res.WriteAsJsonAsync(new ApiEnvelope<T>
        {
            Success = true,
            Data = data,
            Paging = paging
        }, ct);
        return res;
    }

    public static async Task<HttpResponseData> WriteFailureAsync<T>(
        this HttpRequestData req,
        HttpStatusCode statusCode,
        T data,
        CancellationToken ct)
    {
        var res = req.CreateResponse(statusCode);
        await res.WriteAsJsonAsync(new ApiEnvelope<T>
        {
            Success = false,
            Data = data
        }, ct);
        return res;
    }

    public static ApiPaging SinglePage(int totalItems) => new()
    {
        Page = 1,
        PageSize = totalItems,
        TotalItems = totalItems,
        TotalPages = totalItems == 0 ? 0 : 1
    };
}