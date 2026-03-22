namespace AFH.Location.Service.Infrastructure.Persistence.PolicyStore.Entities;

public sealed class IntegrationOperationAuditEntity
{
    public long Id { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public string? CorrelationId { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedUtc { get; set; }
}
