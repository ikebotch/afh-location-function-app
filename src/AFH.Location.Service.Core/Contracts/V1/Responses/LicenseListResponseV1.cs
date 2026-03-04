namespace AFH.Location.Service.Core.Contracts.V1.Responses;

public sealed class LicenseListResponseV1
{
    public IReadOnlyList<string> Licenses { get; init; } = [];
}
