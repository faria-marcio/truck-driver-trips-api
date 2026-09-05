namespace TruckDriverTrips.Api.Dtos.Trips;

public sealed record TripResponse(
    Guid Id,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal DistanceKm,
    string PickupLocation,
    string DropoffLocation,
    string DriverId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
