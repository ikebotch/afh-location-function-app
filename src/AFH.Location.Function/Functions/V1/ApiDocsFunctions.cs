using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using AFH.Location.Function.Functions.V1.Docs;

namespace AFH.Location.Function.Functions.V1;

[LocationOpenApiExclude]
public sealed class ApiDocsFunctions
{
    [Function("OpenApiV1")]
    public async Task<HttpResponseData> OpenApi(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "openapi/v1.json")] HttpRequestData req)
    {
        var json = LocationOpenApiDocumentFactory.CreateOpenApiJson(req.Url);

        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await res.WriteStringAsync(json);
        return res;
    }

    [Function("ScalarUi")]
    public async Task<HttpResponseData> Scalar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scalar")] HttpRequestData req)
    {
        var html = """
                   <!doctype html>
                   <html lang="en">
                   <head>
                     <meta charset="utf-8" />
                     <meta name="viewport" content="width=device-width,initial-scale=1" />
                     <title>AFH Location Service Docs</title>
                   </head>
                   <body>
                     <script id="api-reference" data-url="/api/openapi/v1.json"></script>
                     <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
                   </body>
                   </html>
                   """;

        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "text/html; charset=utf-8");
        await res.WriteStringAsync(html);
        return res;
    }
}
