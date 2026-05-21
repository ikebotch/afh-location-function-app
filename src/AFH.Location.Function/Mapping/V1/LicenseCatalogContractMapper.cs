using AFH.Adviser.Application.Licences;
using AFH.Location.Contract.V1.Responses;

namespace AFH.Location.Function.Mapping.V1;

public static class LicenseCatalogContractMapper
{
    public static LicenseListResponseV1 ToContractResponse(LicenseCatalogResult result)
    {
        return new LicenseListResponseV1
        {
            Licenses = result.Licenses
        };
    }
}
