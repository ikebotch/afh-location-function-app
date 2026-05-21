using AFH.Location.Application.Abstractions.Calendar;
using Microsoft.Extensions.Configuration;

namespace AFH.Location.Infrastructure.Services;

public sealed class BusinessTimeZoneProvider : IBusinessTimeZoneProvider
{
    private readonly IConfiguration _configuration;

    public BusinessTimeZoneProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string TimeZoneId =>
        string.IsNullOrWhiteSpace(_configuration["BusinessTime:TimeZone"])
            ? "Europe/London"
            : _configuration["BusinessTime:TimeZone"]!;
}
