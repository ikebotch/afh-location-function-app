namespace AFH.Location.Service.Domain.Errors;

public sealed class DestinationResolveException : Exception
{
    public DestinationResolveException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}