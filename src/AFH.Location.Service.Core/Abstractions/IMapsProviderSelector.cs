namespace AFH.Location.Service.Core.Abstractions;

public interface IMapsProviderSelector
{
    bool IsV2Request();
}