namespace TruckDriverTrips.Api.Dtos.Locations;

/// <summary>Represents a city suggestion returned from the location search.</summary>
public sealed record CitySearchResponse(
    string Name,
    string? City,
    string? State,
    string? Country,
    double Latitude,
    double Longitude);
