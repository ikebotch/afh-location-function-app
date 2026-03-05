using AFH.Location.Service.Api.Contracts;
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
            var res = await req!.WriteFailureAsync(
                HttpStatusCode.UnprocessableEntity,
                new { code = ex.Code, message = ex.Message },
                CancellationToken.None);
            context.GetInvocationResult().Value = res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            var req = await context.GetHttpRequestDataAsync();
            var res = req is null
                ? null
                : await req.WriteFailureAsync(
                    HttpStatusCode.InternalServerError,
                    new { code = "INTERNAL_ERROR", message = "Something went wrong." },
                    CancellationToken.None);
            context.GetInvocationResult().Value = res;
        }
    }
}
