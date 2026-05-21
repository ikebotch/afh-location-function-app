using AFH.Adviser.Application.Models.Skills;
using AFH.Location.Contract.V1.Responses;

namespace AFH.Location.Function.Mapping.V1.Adviser;

public static class LicenseCatalogContractMapper
{
    public static LicenseListResponseV1 ToContractResponse(AdviserSkillCatalogResult result)
    {
        return new LicenseListResponseV1
        {
            Licenses = result.Licenses
        };
    }
}
