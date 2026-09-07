namespace AFH.Adviser.Infrastructure.Options;

public sealed class SharePointAdviserOptions
{
    public const string SectionName = "SharePoint:Advisers";

    public string SiteId { get; set; } = string.Empty;
    public string ListId { get; set; } = string.Empty;

    // Field mappings (keep simple and explicit)
    public string AdviserIdField { get; set; } = "field_3";
    public string XPlanAdviserIdField { get; set; } = "Adviser Commpay";
    //public string AdviserIdField { get; set; } = "field_2";
    public string DisplayNameField { get; set; } = "LinkTitle";
    public string NameField { get; set; } = "Title";
    public string EmailField { get; set; } = "field_3";
    //public string SkillsField { get; set; } = "AdviserType";
    public string SkillsField { get; set; } = "License";
    //public string SkillsField { get; set; } = "Postcode_x0020_Area";
    public string RegionField { get; set; } = "Region";


    //public string RegionField { get; set; } = "field_8";
    public string PostcodeField { get; set; } = "field_13";
    public string RatingField { get; set; } = "Rating";
    public string CoverageRadiusMilesField { get; set; } = "CoverageRadiusMiles";
    public string MaxTravelTimeMinutesField { get; set; } = "MaxTravelTimeMinutes";
    public double DefaultRating { get; set; } = 0;
    public string StatusField { get; set; } = "Adviser_x0020_Status";
    public string BioOnWebsiteField { get; set; } = "BioonWebsite_x003f_";
}
