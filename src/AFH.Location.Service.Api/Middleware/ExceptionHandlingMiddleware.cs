using AFH.Location.Service.Core.Domain.Errors;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.Net;

namespace AFH.Location.Service.Api.Middleware;

public sealed class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger) => _logger = logger;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (DestinationResolveException ex)
        {
            var req = await context.GetHttpRequestDataAsync();
            var res = req!.CreateResponse(HttpStatusCode.UnprocessableEntity);
            await res.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message });
            context.GetInvocationResult().Value = res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            var req = await context.GetHttpRequestDataAsync();
            var res = req?.CreateResponse(HttpStatusCode.InternalServerError);
            if (res is not null) await res.WriteStringAsync("Something went wrong.");
            context.GetInvocationResult().Value = res;
        }
    }
}