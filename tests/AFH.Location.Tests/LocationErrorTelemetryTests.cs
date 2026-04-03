using System.Text.Json;
using AFH.Common.Errors.ApplicationInsights.Telemetry;
using AFH.Common.Errors.Builders;
using AFH.Location.Function.Middleware;

namespace AFH.Location.Tests;

public sealed class LocationErrorTelemetryTests
{
    [Fact]
    public void ErrorTelemetryBuilder_BuildsHandledLocationTelemetry()
    {
        var mapping = new LocationExceptionMapper().TryMap(new JsonException("Bad JSON"));
        var record = new ErrorRecordBuilder().Build(mapping.MappingResult);
        var builder = new ErrorTelemetryBuilder(new ErrorTelemetryMapper(), new ErrorTelemetryEnricher());
        var telemetry = builder.Build(record, (properties, _) =>
        {
            properties["afh.service"] = "location";
            properties["afh.function.name"] = "location-test";
        });

        Assert.Equal("afh.common_errors", telemetry.Name);
        Assert.Equal("VALIDATION_ERROR", telemetry.Properties["afh.error.code"]);
        Assert.Equal("Validation", telemetry.Properties["afh.error.category"]);
        Assert.Equal("location", telemetry.Properties["afh.service"]);
        Assert.Equal("location-test", telemetry.Properties["afh.function.name"]);
    }
}
