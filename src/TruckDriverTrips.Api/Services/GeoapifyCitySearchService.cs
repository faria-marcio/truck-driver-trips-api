using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TruckDriverTrips.Api.Dtos.Locations;
using TruckDriverTrips.Api.Options;

namespace TruckDriverTrips.Api.Services;

public sealed class GeoapifyCitySearchService(HttpClient httpClient, IMemoryCache cache, IOptions<GeoapifyOptions> options) : ICitySearchService
{
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RouteCacheDuration = TimeSpan.FromHours(6);

    private readonly HttpClient _httpClient = httpClient;
    private readonly IMemoryCache _cache = cache;
    private readonly GeoapifyOptions _options = options.Value;

    public async Task<IReadOnlyList<CitySearchResponse>> SearchCitiesAsync(string query, CancellationToken cancellationToken)
    {
        var normalizedQuery = query.Trim();
        var cacheKey = $"geoapify:cities:{_options.CountryCode}:{normalizedQuery.ToLowerInvariant()}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SearchCacheDuration;

            var requestUri = QueryHelpers.AddQueryString(
                "v1/geocode/autocomplete",
                new Dictionary<string, string?>
                {
                    ["text"] = normalizedQuery,
                    ["filter"] = $"countrycode:{_options.CountryCode}",
                    ["type"] = "city",
                    ["lang"] = "pt",
                    ["limit"] = "8",
                    ["format"] = "json",
                    ["apiKey"] = _options.ApiKey
                });

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new GeoapifyServiceException(response.StatusCode, responseBody);
            }

            var payload = await response.Content.ReadFromJsonAsync<GeoapifyGeocodingResponse>(cancellationToken);
            return (IReadOnlyList<CitySearchResponse>)(payload?.Results?
                .Where(result =>
                    !string.IsNullOrWhiteSpace(result.Formatted) &&
                    result.Latitude.HasValue &&
                    result.Longitude.HasValue)
                .Select(result => new CitySearchResponse(
                    result.Formatted!,
                    result.City,
                    result.State,
                    result.Country,
                    result.Latitude!.Value,
                    result.Longitude!.Value))
                .ToList() ?? []);
        }) ?? [];
    }

    public async Task<RouteDistanceResponse?> GetRouteDistanceAsync(RouteDistanceRequest request, CancellationToken cancellationToken)
    {
        var cacheKey = FormattableString.Invariant(
            $"geoapify:route:{request.OriginLatitude:F5},{request.OriginLongitude:F5}:{request.DestinationLatitude:F5},{request.DestinationLongitude:F5}");

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = RouteCacheDuration;

            var waypoints = FormattableString.Invariant(
                $"{request.OriginLatitude},{request.OriginLongitude}|{request.DestinationLatitude},{request.DestinationLongitude}");
            var requestUri = QueryHelpers.AddQueryString(
                "v1/routing",
                new Dictionary<string, string?>
                {
                    ["waypoints"] = waypoints,
                    ["mode"] = "drive",
                    ["units"] = "metric",
                    ["lang"] = "pt",
                    ["apiKey"] = _options.ApiKey
                });

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new GeoapifyServiceException(response.StatusCode, responseBody);
            }

            var payload = await response.Content.ReadFromJsonAsync<GeoapifyRoutingResponse>(cancellationToken);
            var route = payload?.Features?.FirstOrDefault()?.Properties;
            return route is null
                ? null
                : new RouteDistanceResponse(
                    decimal.Round((decimal)route.DistanceMeters / 1_000, 1),
                    (int)Math.Round(route.TimeSeconds));
        });
    }

    private sealed record GeoapifyGeocodingResponse([property: JsonPropertyName("results")] IReadOnlyList<GeoapifyGeocodingResult>? Results);

    private sealed record GeoapifyGeocodingResult(
        [property: JsonPropertyName("formatted")] string? Formatted,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("state")] string? State,
        [property: JsonPropertyName("country")] string? Country,
        [property: JsonPropertyName("lat")] double? Latitude,
        [property: JsonPropertyName("lon")] double? Longitude);

    private sealed record GeoapifyRoutingResponse(IReadOnlyList<GeoapifyRouteFeature>? Features);

    private sealed record GeoapifyRouteFeature(GeoapifyRouteProperties? Properties);

    private sealed record GeoapifyRouteProperties(
        [property: JsonPropertyName("distance")] double DistanceMeters,
        [property: JsonPropertyName("time")] double TimeSeconds);
}