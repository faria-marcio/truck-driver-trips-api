namespace TruckDriverTrips.Api.Dtos.Locations;

/// <summary>Represents the estimated driving-route distance and duration.</summary>
public sealed record RouteDistanceResponse(decimal DistanceKm, int DurationSeconds);
