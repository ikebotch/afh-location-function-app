using AFH.Location.Application.Abstractions;
using AFH.Location.Application.Abstractions.Search;
using AFH.Location.Application.Models.V1;
using AFH.Location.Application.Models.V1.Requests;
using AFH.Location.Application.Models.V1.Results;
using AFH.Location.Application.Services.V1;
using AFH.Location.Domain;
using System.Reflection;
using System.Text.Json;

namespace AFH.Location.Tests;

public sealed class LocationSearchAuditEntryFactoryTests
{
    [Fact]
    public void Create_PreservesAuditPayloadShapeAndSelectedCandidateSummary()
    {
        var factory = new LocationSearchAuditEntryFactory();
        var response = new LocationSearchResult
        {
            Candidates =
            [
                new LocationSearchCandidate
                {
                    AdviserId = "adv-1",
                    Rank = 1,
                    Score = 12.5,
                    AdviserRating = 4.8,
                    GoldStar = true,
                    Availability = "Available",
                    Coverage = new CoverageResult { WithinCoverage = true },
                    TravelToClient = new TravelToClientResult { EtaMinutes = 17 },
                    Buffers = new BufferResult
                    {
                        MaxTravelTimeMinutes = 90,
                        CompanyBufferMinutes = 20,
                        TravelBufferMinutes = 15
                    },
                    Reasons =
                    [
                        "ORIGIN_SOURCE_HOME",
                        "TRAVEL_TIME_EXCEEDED",
                        "RANKED"
                    ]
                }
            ]
        };

        var entry = InvokeCreate(factory, BuildContext(response));

        Assert.Equal("adv-1", entry.SelectedAdviserId);
        Assert.Equal(4.8, entry.SelectedAdviserRating);
        Assert.True(entry.SelectedAdviserGoldStar);
        Assert.Equal(17, entry.SelectedTravelMinutes);
        Assert.Equal(90, entry.SelectedMaxTravelTimeMinutes);
        Assert.Equal(20, entry.SelectedCompanyBufferMinutes);
        Assert.Equal(15, entry.SelectedTravelBufferMinutes);
        Assert.Equal("ORIGIN_SOURCE_HOME", entry.SelectedOriginSource);

        using var document = JsonDocument.Parse(entry.PayloadJson);
        var payload = document.RootElement;
        Assert.Equal(JsonValueKind.Array, payload.ValueKind);

        var candidate = Assert.Single(payload.EnumerateArray());
        Assert.Equal("adv-1", candidate.GetProperty("AdviserId").GetString());
        Assert.Equal(1, candidate.GetProperty("Rank").GetInt32());
        Assert.Equal(12.5, candidate.GetProperty("Score").GetDouble());
        Assert.Equal(4.8, candidate.GetProperty("AdviserRating").GetDouble());
        Assert.True(candidate.GetProperty("GoldStar").GetBoolean());
        Assert.Equal("Available", candidate.GetProperty("Availability").GetString());
        Assert.True(candidate.GetProperty("WithinCoverage").GetBoolean());
        Assert.Equal(17, candidate.GetProperty("TravelEtaMinutes").GetInt32());
        Assert.Equal(90, candidate.GetProperty("MaxTravelTimeMinutes").GetInt32());
        Assert.Equal(20, candidate.GetProperty("CompanyBufferMinutes").GetInt32());
        Assert.Equal(15, candidate.GetProperty("TravelBufferMinutes").GetInt32());
        Assert.Equal("ORIGIN_SOURCE_HOME", candidate.GetProperty("OriginSource").GetString());

        var failureReasons = candidate.GetProperty("FailureReasons").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(["TRAVEL_TIME_EXCEEDED"], failureReasons);
        Assert.False(candidate.TryGetProperty("MailboxUserId", out _));
        Assert.False(candidate.TryGetProperty("TravelToBase", out _));
        Assert.False(candidate.TryGetProperty("TravelToNearestOffice", out _));
    }

    private static SearchAuditEntry InvokeCreate(LocationSearchAuditEntryFactory factory, object context)
    {
        var createMethod = typeof(LocationSearchAuditEntryFactory).GetMethod(
            "Create",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(createMethod);
        var entry = createMethod.Invoke(factory, [context]);
        return Assert.IsType<SearchAuditEntry>(entry);
    }

    private static object BuildContext(LocationSearchResult response)
    {
        var contextType = typeof(LocationSearchAuditEntryFactory).Assembly
            .GetType("AFH.Location.Application.Services.V1.LocationSearchContext");

        Assert.NotNull(contextType);

        var context = Activator.CreateInstance(contextType!, nonPublic: true);
        Assert.NotNull(context);

        SetProperty(context, "Request", new LocationSearchRequest
        {
            RequestId = "req-audit",
            Destination = new SearchDestination
            {
                Address = new SearchAddress
                {
                    Postcode = "AB1 2CD"
                }
            },
            Meeting = new LocationMeetingWindow
            {
                RequestedStartUtc = new DateTime(2026, 04, 06, 10, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 60,
                SearchHorizonMinutes = 120
            },
            Filters = new LocationSearchFilters
            {
                Regions = ["Region-1", "Region-2"]
            }
        });
        SetProperty(context, "Response", response);
        SetProperty(context, "Destination", (51.600001d, -0.200001d));
        SetProperty(context, "DestResolved", new DestinationResolved
        {
            Lat = 51.600001d,
            Lng = -0.200001d,
            Source = DestinationSource.GeocodedAddress,
            NormalisedAddress = "AB1 2CD"
        });

        return context;
    }

    private static void SetProperty(object instance, string propertyName, object value)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property.SetValue(instance, value);
    }
}