using AFH.Location.Service.Api.Mappings.V1;
using AFH.Location.Service.Contract.V1.Requests;

namespace AFH.Location.Service.UnitTests;

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
}
