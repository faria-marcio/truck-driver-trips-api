namespace TruckDriverTrips.Api.Dtos.Trips;

/// <summary>Aggregated trip totals for the authorized and filtered trip scope.</summary>
public sealed record TripSummaryResponse(
    int Count,
    decimal TotalDistanceKm,
    decimal TotalCommissionAmount,
    decimal CommissionPerKm);
