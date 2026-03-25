using AFH.Location.Service.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace AFH.Location.Service.Infrastructure.Services;

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
