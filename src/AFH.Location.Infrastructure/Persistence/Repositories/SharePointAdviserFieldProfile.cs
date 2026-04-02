using AFH.Common.SharePointUtils.Abstractions;
using AFH.Common.SharePointUtils.Models;
using AFH.Location.Infrastructure.Options;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

internal sealed class SharePointAdviserFieldProfile : ISharePointMappingProfile
{
    public SharePointAdviserFieldProfile(SharePointAdviserOptions options)
    {
        Fields =
        [
            Map(nameof(SharePointAdviserFieldNames.AdviserId), options.AdviserIdField),
            Map(nameof(SharePointAdviserFieldNames.DisplayName), options.DisplayNameField),
            Map(nameof(SharePointAdviserFieldNames.Name), options.NameField),
            Map(nameof(SharePointAdviserFieldNames.Email), options.EmailField),
            Map(nameof(SharePointAdviserFieldNames.Skills), options.SkillsField),
            Map(nameof(SharePointAdviserFieldNames.Region), options.RegionField),
            Map(nameof(SharePointAdviserFieldNames.Postcode), options.PostcodeField),
            Map(nameof(SharePointAdviserFieldNames.Rating), options.RatingField),
            Map(nameof(SharePointAdviserFieldNames.CoverageRadiusMiles), options.CoverageRadiusMilesField),
            Map(nameof(SharePointAdviserFieldNames.MaxTravelTimeMinutes), options.MaxTravelTimeMinutesField),
            Map(nameof(SharePointAdviserFieldNames.Status), options.StatusField),
            Map(nameof(SharePointAdviserFieldNames.BioOnWebsite), options.BioOnWebsiteField)
        ];
    }

    public IReadOnlyCollection<SharePointFieldMap> Fields { get; }

    private static SharePointFieldMap Map(string propertyName, string fieldName) => new()
    {
        PropertyName = propertyName,
        FieldName = fieldName,
        NameKind = SharePointFieldNameKind.Auto
    };
}

internal static class SharePointAdviserFieldNames
{
    public const string AdviserId = nameof(AdviserId);
    public const string DisplayName = nameof(DisplayName);
    public const string Name = nameof(Name);
    public const string Email = nameof(Email);
    public const string Skills = nameof(Skills);
    public const string Region = nameof(Region);
    public const string Postcode = nameof(Postcode);
    public const string Rating = nameof(Rating);
    public const string CoverageRadiusMiles = nameof(CoverageRadiusMiles);
    public const string MaxTravelTimeMinutes = nameof(MaxTravelTimeMinutes);
    public const string Status = nameof(Status);
    public const string BioOnWebsite = nameof(BioOnWebsite);
}
