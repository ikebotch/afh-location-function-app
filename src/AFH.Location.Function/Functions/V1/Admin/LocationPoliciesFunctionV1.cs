using System.Globalization;
using System.Net;
using System.Text.Json;
using AFH.Adviser.Application.Models.Auth;
using AFH.Location.Function.Functions.Common;
using AFH.Location.Function.Security;
using AFH.Location.Infrastructure.Persistence.PolicyStore;
using AFH.Location.Infrastructure.Persistence.PolicyStore.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace AFH.Location.Function.Functions.V1.Admin;

public sealed class LocationPoliciesFunctionV1
{
    private const string RouteMaxOriginsKey = "RouteMatrix.MaxOriginsPerCall";
    private const string RouteMaxDestinationsKey = "RouteMatrix.MaxDestinationsPerCall";
    private const string RouteSuccessConfidenceKey = "RouteMatrix.SuccessConfidence";
    private const string RouteFailureConfidenceKey = "RouteMatrix.FailureConfidence";
    private const string GeoSuccessTtlKey = "GeoCache.SuccessTtlMinutes";
    private const string GeoFailureTtlKey = "GeoCache.FailureTtlMinutes";
    private const string RouteCacheTtlKey = "RouteCache.SuccessTtlMinutes";

    private readonly LocationPolicyDbContext _db;
    private readonly IDomainUserAuthorizationService _auth;

    public LocationPoliciesFunctionV1(LocationPolicyDbContext db, IDomainUserAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    [Function("LocationPolicyDefaultsGetV1")]
    public async Task<HttpResponseData> GetDefaultsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/location/policies/defaults")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.Read, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var defaults = await GetOrCreateDefaultsAsync(ct);
        return await req.WriteSuccessAsync(ToDefaultsResponse(defaults), ct);
    }

    [Function("LocationPolicyDefaultsPatchV1")]
    public async Task<HttpResponseData> PatchDefaultsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/location/policies/defaults")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.Update, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<LocationPolicyDefaultsPatchRequestV1>(ct);
        if (body is null)
            return await ValidationAsync(req, "Request body is required.", ct);

        if (body.GlobalDistanceMiles is <= 0 or > 500)
            return await ValidationAsync(req, "globalDistanceMiles must be greater than 0 and no more than 500.", ct);
        if (body.GlobalMaxTravelTimeMinutes is <= 0 or > 600)
            return await ValidationAsync(req, "globalMaxTravelTimeMinutes must be greater than 0 and no more than 600.", ct);

        var defaults = await GetOrCreateDefaultsAsync(ct);
        if (body.GlobalDistanceMiles.HasValue)
            defaults.DefaultRadiusMiles = body.GlobalDistanceMiles.Value;
        if (body.GlobalMaxTravelTimeMinutes.HasValue)
            defaults.DefaultMaxTravelTimeMinutes = body.GlobalMaxTravelTimeMinutes.Value;

        AddAudit(req, "Admin.LocationPolicyDefaults.Update", "LocationPolicyDefaultsUpdated", body);
        await _db.SaveChangesAsync(ct);
        return await req.WriteSuccessAsync(ToDefaultsResponse(defaults, DateTime.UtcNow), ct);
    }

    [Function("LocationPolicyRouteMatrixGetV1")]
    public async Task<HttpResponseData> GetRouteMatrixAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/location/policies/route-matrix")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.Read, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var settings = await ReadSettingsAsync(ct);
        return await req.WriteSuccessAsync(ToRouteMatrixResponse(settings), ct);
    }

    [Function("LocationPolicyRouteMatrixPatchV1")]
    public async Task<HttpResponseData> PatchRouteMatrixAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/location/policies/route-matrix")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.Update, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<RouteMatrixPolicyPatchRequestV1>(ct);
        if (body is null)
            return await ValidationAsync(req, "Request body is required.", ct);

        if (body.MaxOriginsPerCall is <= 0 or > 250)
            return await ValidationAsync(req, "maxOriginsPerCall must be between 1 and 250.", ct);
        if (body.MaxDestinationsPerCall is <= 0 or > 250)
            return await ValidationAsync(req, "maxDestinationsPerCall must be between 1 and 250.", ct);

        var now = DateTime.UtcNow;
        if (body.MaxOriginsPerCall.HasValue)
            await UpsertSettingAsync(RouteMaxOriginsKey, body.MaxOriginsPerCall.Value.ToString(CultureInfo.InvariantCulture), now, ct);
        if (body.MaxDestinationsPerCall.HasValue)
            await UpsertSettingAsync(RouteMaxDestinationsKey, body.MaxDestinationsPerCall.Value.ToString(CultureInfo.InvariantCulture), now, ct);
        if (!string.IsNullOrWhiteSpace(body.SuccessConfidence))
            await UpsertSettingAsync(RouteSuccessConfidenceKey, body.SuccessConfidence.Trim(), now, ct);
        if (!string.IsNullOrWhiteSpace(body.FailureConfidence))
            await UpsertSettingAsync(RouteFailureConfidenceKey, body.FailureConfidence.Trim(), now, ct);

        AddAudit(req, "Admin.RouteMatrixPolicy.Update", "RouteMatrixPolicyUpdated", body);
        await _db.SaveChangesAsync(ct);
        var settings = await ReadSettingsAsync(ct);
        return await req.WriteSuccessAsync(ToRouteMatrixResponse(settings, now), ct);
    }

    [Function("LocationPolicyGeoCacheGetV1")]
    public async Task<HttpResponseData> GetGeoCacheAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/location/policies/geo-cache")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.Read, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var settings = await ReadSettingsAsync(ct);
        return await req.WriteSuccessAsync(ToGeoCacheResponse(settings), ct);
    }

    [Function("LocationPolicyGeoCachePatchV1")]
    public async Task<HttpResponseData> PatchGeoCacheAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/location/policies/geo-cache")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var authFailure = await _auth.AuthorizeAsync(req, CoverageRegionPermissions.Update, allowInternal: true, ct);
        if (authFailure is not null)
            return authFailure;

        var body = await req.ReadFromJsonAsync<GeoCachePolicyPatchRequestV1>(ct);
        if (body is null)
            return await ValidationAsync(req, "Request body is required.", ct);

        if (body.GeoSuccessTtlMinutes is <= 0 or > 43200)
            return await ValidationAsync(req, "geoSuccessTtlMinutes must be between 1 and 43200.", ct);
        if (body.GeoFailureTtlMinutes is <= 0 or > 43200)
            return await ValidationAsync(req, "geoFailureTtlMinutes must be between 1 and 43200.", ct);
        if (body.RouteCacheTtlMinutes is <= 0 or > 43200)
            return await ValidationAsync(req, "routeCacheTtlMinutes must be between 1 and 43200.", ct);

        var now = DateTime.UtcNow;
        if (body.GeoSuccessTtlMinutes.HasValue)
            await UpsertSettingAsync(GeoSuccessTtlKey, body.GeoSuccessTtlMinutes.Value.ToString(CultureInfo.InvariantCulture), now, ct);
        if (body.GeoFailureTtlMinutes.HasValue)
            await UpsertSettingAsync(GeoFailureTtlKey, body.GeoFailureTtlMinutes.Value.ToString(CultureInfo.InvariantCulture), now, ct);
        if (body.RouteCacheTtlMinutes.HasValue)
            await UpsertSettingAsync(RouteCacheTtlKey, body.RouteCacheTtlMinutes.Value.ToString(CultureInfo.InvariantCulture), now, ct);

        AddAudit(req, "Admin.GeoCachePolicy.Update", "GeoCachePolicyUpdated", body);
        await _db.SaveChangesAsync(ct);
        var settings = await ReadSettingsAsync(ct);
        return await req.WriteSuccessAsync(ToGeoCacheResponse(settings, now), ct);
    }

    private async Task<CoverageDefaultPolicyEntity> GetOrCreateDefaultsAsync(CancellationToken ct)
    {
        var defaults = await _db.CoverageDefaults.FirstOrDefaultAsync(ct);
        if (defaults is not null)
            return defaults;

        defaults = new CoverageDefaultPolicyEntity
        {
            DefaultRadiusMiles = 100,
            DefaultMaxTravelTimeMinutes = 90
        };
        _db.CoverageDefaults.Add(defaults);
        await _db.SaveChangesAsync(ct);
        return defaults;
    }

    private async Task<Dictionary<string, string>> ReadSettingsAsync(CancellationToken ct)
        => await _db.PolicySettings.AsNoTracking()
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase, ct);

    private async Task UpsertSettingAsync(string key, string value, DateTime now, CancellationToken ct)
    {
        var setting = await _db.PolicySettings.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (setting is null)
        {
            setting = new LocationPolicySettingEntity { Key = key };
            _db.PolicySettings.Add(setting);
        }

        setting.Value = value;
        setting.UpdatedUtc = now;
    }

    private void AddAudit(HttpRequestData req, string operation, string eventType, object payload)
    {
        var now = DateTime.UtcNow;
        _db.ApplicationLogs.Add(new ApplicationLogEntity
        {
            OccurredUtc = now,
            CreatedUtc = now,
            Level = "Information",
            Category = "Settings",
            Operation = operation,
            UserId = GetActor(req),
            EventType = eventType,
            Result = "Succeeded",
            Message = "Location policy settings were updated.",
            PayloadJson = JsonSerializer.Serialize(payload)
        });
    }

    private static LocationPolicyDefaultsResponseV1 ToDefaultsResponse(CoverageDefaultPolicyEntity entity, DateTime? updatedUtc = null)
        => new(entity.DefaultRadiusMiles, entity.DefaultMaxTravelTimeMinutes, updatedUtc, "CoverageDefaultPolicies");

    private static RouteMatrixPolicyResponseV1 ToRouteMatrixResponse(IReadOnlyDictionary<string, string> settings, DateTime? updatedUtc = null)
        => new(
            ReadInt(settings, RouteMaxOriginsKey, 50),
            ReadInt(settings, RouteMaxDestinationsKey, 50),
            ReadString(settings, RouteSuccessConfidenceKey, "High"),
            ReadString(settings, RouteFailureConfidenceKey, "Low"),
            updatedUtc,
            "LocationPolicySettings");

    private static GeoCachePolicyResponseV1 ToGeoCacheResponse(IReadOnlyDictionary<string, string> settings, DateTime? updatedUtc = null)
        => new(
            ReadInt(settings, GeoSuccessTtlKey, 10080),
            ReadInt(settings, GeoFailureTtlKey, 30),
            ReadInt(settings, RouteCacheTtlKey, 1440),
            updatedUtc,
            "LocationPolicySettings");

    private static int ReadInt(IReadOnlyDictionary<string, string> settings, string key, int fallback)
        => settings.TryGetValue(key, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static string ReadString(IReadOnlyDictionary<string, string> settings, string key, string fallback)
        => settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    private static Task<HttpResponseData> ValidationAsync(HttpRequestData req, string message, CancellationToken ct)
        => req.WriteFailureAsync(HttpStatusCode.BadRequest, new { code = "VALIDATION", message }, ct);

    private static string? GetActor(HttpRequestData req)
        => req.Headers.TryGetValues("x-afh-user-profile-id", out var profileIds)
            ? profileIds.FirstOrDefault()
            : req.Headers.TryGetValues("x-user", out var users)
                ? users.FirstOrDefault()
                : null;
}

public sealed record LocationPolicyDefaultsPatchRequestV1(double? GlobalDistanceMiles, int? GlobalMaxTravelTimeMinutes);

public sealed record LocationPolicyDefaultsResponseV1(
    double GlobalDistanceMiles,
    int GlobalMaxTravelTimeMinutes,
    DateTime? UpdatedUtc,
    string Source);

public sealed record RouteMatrixPolicyPatchRequestV1(
    int? MaxOriginsPerCall,
    int? MaxDestinationsPerCall,
    string? SuccessConfidence,
    string? FailureConfidence);

public sealed record RouteMatrixPolicyResponseV1(
    int MaxOriginsPerCall,
    int MaxDestinationsPerCall,
    string SuccessConfidence,
    string FailureConfidence,
    DateTime? UpdatedUtc,
    string Source);

public sealed record GeoCachePolicyPatchRequestV1(
    int? GeoSuccessTtlMinutes,
    int? GeoFailureTtlMinutes,
    int? RouteCacheTtlMinutes);

public sealed record GeoCachePolicyResponseV1(
    int GeoSuccessTtlMinutes,
    int GeoFailureTtlMinutes,
    int RouteCacheTtlMinutes,
    DateTime? UpdatedUtc,
    string Source);
