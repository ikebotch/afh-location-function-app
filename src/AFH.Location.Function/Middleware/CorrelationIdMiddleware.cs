using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace AFH.Location.Function.Middleware;

public sealed class CorrelationIdMiddleware : IFunctionsWorkerMiddleware
{
    public const string HeaderName = "x-correlation-id";
    public const string ItemKey = "correlation-id";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();
        if (req is not null)
        {
            var cid = req.Headers.TryGetValues(HeaderName, out var values)
                ? values.FirstOrDefault()
                : null;

            context.Items[ItemKey] = string.IsNullOrWhiteSpace(cid)
                ? Guid.NewGuid().ToString("N")
                : cid!;
        }

        await next(context);

        var response = context.GetInvocationResult().Value as HttpResponseData;
        if (response is not null &&
            context.Items.TryGetValue(ItemKey, out var value) &&
            value is string correlationId &&
            !response.Headers.TryGetValues(HeaderName, out _))
        {
            response.Headers.Add(HeaderName, correlationId);
        }
    }
}
