using System.ComponentModel.DataAnnotations;

namespace TruckDriverTrips.Api.Dtos.Trips;

/// <summary>Optional filters and pagination controls for trip list and summary queries.</summary>
public sealed record TripQueryRequest
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    [MaxLength(100)]
    public string? TruckId { get; init; }

    [Range(1, 1_000_000)]
    public int? Page { get; init; }

    [Range(1, 100)]
    public int? PageSize { get; init; }
}
