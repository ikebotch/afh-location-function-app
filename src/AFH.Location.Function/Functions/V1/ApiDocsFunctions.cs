using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;

namespace AFH.Location.Function.Functions.V1;

public sealed class ApiDocsFunctions
{
    [Function("OpenApiV1")]
    public async Task<HttpResponseData> OpenApi(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "openapi/v1.json")] HttpRequestData req)
    {
        var baseUrl = $"{req.Url.Scheme}://{req.Url.Host}";
        if (!req.Url.IsDefaultPort) baseUrl += $":{req.Url.Port}";

        var doc = new
        {
            openapi = "3.0.1",
            info = new
            {
                title = "AFH Location Service API",
                version = "v1",
                description = "Location adviser discovery and ranking API."
            },
            servers = new[]
            {
                new { url = $"{baseUrl}/api" }
            },
            paths = new Dictionary<string, object>
            {
                ["/v1/location/health"] = new
                {
                    get = new
                    {
                        tags = new[] { "Health" },
                        summary = "Health check",
                        responses = new Dictionary<string, object>
                        {
                            ["200"] = new { description = "Service healthy" }
                        }
                    }
                },
                ["/v1/location/inperson/advisers/search"] = new
                {
                    post = new
                    {
                        tags = new[] { "LocationSearch" },
                        summary = "Search in-person advisers",
                        requestBody = new
                        {
                            required = true,
                            content = new Dictionary<string, object>
                            {
                                ["application/json"] = new
                                {
                                    schema = new { type = "object" }
                                }
                            }
                        },
                        responses = new Dictionary<string, object>
                        {
                            ["200"] = new
                            {
                                description = "Ranked adviser candidates",
                                content = new Dictionary<string, object>
                                {
                                    ["application/json"] = new { schema = new { type = "object" } }
                                }
                            },
                            ["400"] = new { description = "Validation error" },
                            ["401"] = new { description = "Unauthorized" },
                            ["500"] = new { description = "Server error" }
                        }
                    }
                },
                ["/v1/admin/adviser-coverage"] = new
                {
                    get = new
                    {
                        tags = new[] { "Admin" },
                        summary = "Get adviser and region coverage points for dashboard mapping",
                        responses = new Dictionary<string, object>
                        {
                            ["200"] = new { description = "Coverage dataset" }
                        }
                    }
                },
                ["/v2/location/inperson/advisers/search"] = new
                {
                    post = new
                    {
                        tags = new[] { "LocationSearchV2" },
                        summary = "Search in-person advisers (v2)",
                        responses = new Dictionary<string, object>
                        {
                            ["200"] = new { description = "Success" }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        });

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
