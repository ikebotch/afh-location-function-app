//using AFH.Location.Service.Core.Contracts.V1.Responses;
//using AFH.Location.Service.Core.Domain;
//using AFH.Location.Service.Core.Domain.Entities;

//namespace AFH.Location.Service.Core.Mapping;

//public static class AdviserMappingExtensions
//{
//    public static AdviserProfile ToDto(this Adviser adviser)
//    {
//        return new AdviserProfile
//        {
//            AdviserId = adviser.AdviserId,
//            DisplayName = adviser.DisplayName,
//            Region = adviser.Region,
//            Skills = adviser.Skills?.ToList() ?? [],
//            HomePostcode = adviser.HomePostcode,
//            Email = adviser.Email,
//            Team = adviser.Team,
//            Grade = adviser.Grade,
//            IsActive = adviser.IsActive
//        };
//    }
//}