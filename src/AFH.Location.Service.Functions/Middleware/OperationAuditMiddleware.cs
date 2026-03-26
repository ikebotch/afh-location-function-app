using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Service.Infrastructure.Persistence.PolicyStore.Entities;
using System.Diagnostics;
using System.Net;

namespace AFH.Location.Service.Api.Middleware;

public sealed class OperationAuditMiddleware : IFunctionsWorkerMiddleware
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OperationAuditMiddleware> _logger;

    public OperationAuditMiddleware(
        IServiceScopeFactory scopeFactory,
        ILogger<OperationAuditMiddleware> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();
        if (req is null)
        {
            await next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        Exception? unhandled = null;
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            unhandled = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            var response = context.GetInvocationResult().Value as HttpResponseData;
            var statusCode = (int)(response?.StatusCode ?? (unhandled is null ? HttpStatusCode.OK : HttpStatusCode.InternalServerError));

            context.Items.TryGetValue(CorrelationIdMiddleware.Header, out var cid);
            var correlationId = cid?.ToString();

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetService<LocationPolicyDbContext>();
                if (db is not null)
                {
                    db.IntegrationOperationAudits.Add(new IntegrationOperationAuditEntity
                    {
                        ServiceName = "location",
                        FunctionName = context.FunctionDefinition.Name,
                        Method = req.Method,
                        Path = req.Url.AbsolutePath,
                        QueryString = req.Url.Query,
                        CorrelationId = correlationId,
                        OperationId = context.InvocationId,
                        StatusCode = statusCode,
                        DurationMs = sw.ElapsedMilliseconds,
                        ErrorType = unhandled?.GetType().Name,
                        ErrorMessage = unhandled?.Message,
                        CreatedUtc = DateTime.UtcNow
                    });

                    await db.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist location operation audit.");
            }

            _logger.LogInformation(
                "operation_audit service={Service} function={Function} method={Method} path={Path} status={StatusCode} durationMs={DurationMs} correlationId={CorrelationId} operationId={OperationId} errorType={ErrorType}",
                "location",
                context.FunctionDefinition.Name,
                req.Method,
                req.Url.AbsolutePath,
                statusCode,
                sw.ElapsedMilliseconds,
                correlationId,
                context.InvocationId,
                unhandled?.GetType().Name);
        }
    }
}
