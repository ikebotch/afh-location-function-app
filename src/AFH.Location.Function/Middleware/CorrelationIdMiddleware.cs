using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace AFH.Location.Function.Middleware;

public sealed class CorrelationIdMiddleware : IFunctionsWorkerMiddleware
{
    public const string Header = "x-correlation-id";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();
        if (req is not null)
        {
            var cid = req.Headers.TryGetValues(Header, out var values)
                ? values.FirstOrDefault()
                : null;

            context.Items[Header] = string.IsNullOrWhiteSpace(cid)
                ? Guid.NewGuid().ToString("N")
                : cid!;
        }

        await next(context);

        var response = context.GetInvocationResult().Value as HttpResponseData;
        if (response is not null &&
            context.Items.TryGetValue(Header, out var value) &&
            value is string correlationId &&
            !response.Headers.TryGetValues(Header, out _))
        {
            response.Headers.Add(Header, correlationId);
        }
    }
}
