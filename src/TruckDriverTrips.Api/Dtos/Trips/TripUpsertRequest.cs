using System.ComponentModel.DataAnnotations;

namespace TruckDriverTrips.Api.Dtos.Trips;

public sealed class TripUpsertRequest
{
    [Required]
    public DateOnly? Date { get; init; }

    [Required]
    public TimeOnly? StartTime { get; init; }

    [Required]
    public TimeOnly? EndTime { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal DistanceKm { get; init; }

    [Required]
    [MaxLength(250)]
    public string PickupLocation { get; init; } = string.Empty;

    [Required]
    [MaxLength(250)]
    public string DropoffLocation { get; init; } = string.Empty;
}
