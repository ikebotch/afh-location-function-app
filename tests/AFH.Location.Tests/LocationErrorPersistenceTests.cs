using System.Text.Json;
using AFH.Common.Errors.Builders;
using AFH.Common.Errors.EntityFramework.Entities;
using AFH.Common.Errors.EntityFramework.Persistence;
using AFH.Location.Function.Middleware;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Tests;

public sealed class LocationErrorPersistenceTests
{
    [Fact]
    public void LocationPolicyDbContext_ModelIncludesSharedErrorRecordEntity()
    {
        using var dbContext = CreateDbContext();

        var entityType = dbContext.Model.FindEntityType(typeof(ErrorRecordEntity));

        Assert.NotNull(entityType);
    }

    [Fact]
    public async Task EntityFrameworkErrorPersistenceWriter_PersistsHandledLocationErrorRecord()
    {
        using var dbContext = CreateDbContext();
        var mapping = new LocationExceptionMapper().TryMap(new JsonException("Bad JSON"));
        var record = new ErrorRecordBuilder().Build(mapping.MappingResult);
        var writer = new EntityFrameworkErrorPersistenceWriter<LocationPolicyDbContext>(dbContext);

        await writer.WriteAsync(record);

        var persisted = await dbContext.Set<ErrorRecordEntity>().SingleAsync();
        Assert.Equal("VALIDATION_ERROR", persisted.Code);
        Assert.Equal("Validation", persisted.Category);
        Assert.Equal("Warning", persisted.Severity);
        Assert.Equal("Invalid JSON payload.", persisted.Message);
    }

    private static LocationPolicyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LocationPolicyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new LocationPolicyDbContext(options);
    }
}
