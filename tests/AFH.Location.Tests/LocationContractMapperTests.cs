using AFH.Location.Function.Mapping.V1;
using AFH.Location.Application.Models.V1;
using AFH.Location.Contract.V1.Requests;

namespace AFH.Location.Tests;

public class LocationContractMapperTests
{
    [Fact]
    public void ToApplicationRequest_MapsContractPayloadWithoutLeakingTransportTypes()
    {
        var request = new LocationSearchRequestV1
        {
            RequestId = "req-123",
            Meeting = new MeetingWindow
            {
                RequestedStartUtc = new DateTime(2026, 03, 25, 9, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 60,
                SearchHorizonMinutes = 180
            },
            Destination = new Destination
            {
                Address = new Address
                {
                    Line1 = "1 Example Street",
                    Town = "Birmingham",
                    Postcode = "B1 1AA",
                    Country = "UK"
                }
            },
            Filters = new LocationSearchFilters
            {
                Regions = ["West Midlands"],
                PreferredAdviserIds = ["adv-1"],
                MaxCandidates = 5
            }
        };

        var mapped = LocationContractMapper.ToApplicationRequest(request);

        Assert.Equal(request.RequestId, mapped.RequestId);
        Assert.Equal(request.Meeting.RequestedStartUtc, mapped.Meeting.RequestedStartUtc);
        Assert.Equal(request.Meeting.DurationMinutes, mapped.Meeting.DurationMinutes);
        Assert.Equal(request.Destination.Address!.Postcode, mapped.Destination.Address!.Postcode);
        Assert.Equal("West Midlands", Assert.Single(mapped.Filters.Regions));
        Assert.Equal("adv-1", Assert.Single(mapped.Filters.PreferredAdviserIds));
        Assert.Equal(5, mapped.Filters.MaxCandidates);
    }

    [Fact]
    public void ToContractResponse_MapsMailboxIdentityOnCoverageAndSearchCandidates()
    {
        var search = LocationContractMapper.ToContractResponse(new LocationSearchResult
        {
            RequestId = "req-1",
            GeneratedAtUtc = new DateTime(2026, 04, 02, 10, 0, 0, DateTimeKind.Utc),
            Candidates =
            [
                new LocationSearchCandidate
                {
                    AdviserId = "adv-1",
                    MailboxUserId = "adviser.one@tenant.com",
                    AdviserRating = 4.7,
                    Preferred = true
                }
            ]
        });

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

        Assert.Equal("adviser.one@tenant.com", Assert.Single(search.Candidates).MailboxUserId);
        var adviser = Assert.Single(coverage.Advisers);
        Assert.Equal("adviser.one@tenant.com", adviser.MailboxUserId);
        Assert.True(adviser.IsActive);
        Assert.Equal("Equity Release", Assert.Single(adviser.Skills));
        Assert.Equal(4.7, adviser.Rating);
    }
}
