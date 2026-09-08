using TruckDriverTrips.Api.Dtos.Locations;

namespace TruckDriverTrips.Api.Services;

public interface ICitySearchService
{
    Task<IReadOnlyList<CitySearchResponse>> SearchCitiesAsync(string query, CancellationToken cancellationToken);

    Task<RouteDistanceResponse?> GetRouteDistanceAsync(RouteDistanceRequest request, CancellationToken cancellationToken);
}