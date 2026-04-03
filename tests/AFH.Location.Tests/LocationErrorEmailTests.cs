using System.Text.Json;
using AFH.Common.Errors.Builders;
using AFH.Common.Errors.Email.Builders;
using AFH.Common.Errors.Email.Options;
using AFH.Location.Function.Middleware;

namespace AFH.Location.Tests;

public sealed class LocationErrorEmailTests
{
    [Fact]
    public void LocationHandledErrorEmailPolicy_DoesNotNotifyForValidationErrors()
    {
        var mapping = new LocationExceptionMapper().TryMap(new JsonException("Bad JSON"));

        Assert.False(LocationHandledErrorEmailPolicy.ShouldNotify(mapping.MappingResult));
    }

    [Fact]
    public void LocationHandledErrorEmailPolicy_BuildsSharedEmailForServerErrors()
    {
        var mapping = new LocationExceptionMapper().TryMap(new Exception("Boom"));

        Assert.True(LocationHandledErrorEmailPolicy.ShouldNotify(mapping.MappingResult));

        var record = new ErrorRecordBuilder().Build(mapping.MappingResult);
        var request = LocationHandledErrorEmailPolicy.CreateNotificationRequest("LocationFunction", mapping.MappingResult.StatusCode, record);
        var builder = new ErrorEmailMessageBuilder();
        var model = builder.BuildTemplateModel(request, new ErrorEmailOptions
        {
            ToAddresses = ["ops@example.com"],
            SubjectPrefix = "[AFH Location Error]"
        });
        var body = builder.BuildBody(model);

        Assert.Equal("[AFH Location Error] Error: INTERNAL_ERROR", model.Subject);
        Assert.Equal("location", model.Metadata["service"]);
        Assert.Equal("500", model.Metadata["statusCode"]);
        Assert.Contains("Location handled exception in LocationFunction.", body);
    }
}
