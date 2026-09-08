namespace TruckDriverTrips.Api.Dtos.Trips;

/// <summary>Represents a trip with server-managed ownership, distance, timestamps, and version.</summary>
public sealed record TripResponse(
    Guid Id,
    DateOnly Date,
    string TruckId,
    decimal StartKm,
    decimal EndKm,
    decimal DistanceKm,
    string PickupLocation,
    string DropoffLocation,
    decimal CommissionAmount,
    string? BolNumber,
    decimal FuelCostAmount,
    int WaitTimeMinutes,
    string? Notes,
    string DriverId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int Version);
