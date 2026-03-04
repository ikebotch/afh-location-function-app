using AFH.Location.Service.Core.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace AFH.Location.Service.Api.Middleware;

public sealed class CorrelationIdMiddleware : IFunctionsWorkerMiddleware
{
    private const string Header = "x-correlation-id";



    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();
        if (req is not null)
        {
            RequestContextAccessor.SetPath(req.Url.AbsolutePath);

            var cid = req.Headers.TryGetValues("x-correlation-id", out var values)
                ? values.FirstOrDefault()
                : null;

            context.Items["x-correlation-id"] =
                string.IsNullOrWhiteSpace(cid) ? Guid.NewGuid().ToString() : cid!;
        }

        await next(context);
    }
}