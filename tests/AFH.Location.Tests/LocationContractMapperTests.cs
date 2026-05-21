using AFH.Location.Application.Models.V1;
using AFH.Location.Application.Models.V1.Results;
using AFH.Location.Function.Mapping.V1;

namespace AFH.Location.Tests;

public class LocationContractMapperTests
{
    [Fact]
    public void ToContractResponse_MapsMailboxIdentityOnCoverageCandidates()
    {
        var coverage = LocationContractMapper.ToContractResponse(new AdviserCoverageResult
        {
            Advisers =
            [
                new AdviserCoveragePoint
                {
                    Id = "adv-1",
                    Name = "Adviser One",
                    MailboxUserId = "adviser.one@tenant.com",
                    IsActive = true,
                    Skills = ["Equity Release"],
                    Rating = 4.7
                }
            ]
        });

        var adviser = Assert.Single(coverage.Advisers);
        Assert.Equal("adviser.one@tenant.com", adviser.MailboxUserId);
        Assert.True(adviser.IsActive);
        Assert.Equal("Equity Release", Assert.Single(adviser.Skills));
        Assert.Equal(4.7, adviser.Rating);
    }
}
