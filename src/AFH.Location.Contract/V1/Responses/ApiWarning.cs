namespace AFH.Location.Contract.V1.Responses;

public sealed class ApiWarning
{
    public string Code { get; set; } = default!;
    public string Message { get; set; } = default!;
}
