using System.ComponentModel.DataAnnotations;

namespace TruckDriverTrips.Api.Dtos.Trips;

/// <summary>Editable trip fields accepted when creating or replacing a trip.</summary>
public sealed record TripUpsertRequest
{
    [Required]
    public DateOnly? Date { get; init; }

    [Required, MaxLength(100)]
    public string TruckId { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal StartKm { get; init; }

    [Range(0, double.MaxValue)]
    public decimal EndKm { get; init; }

    [Required]
    [MaxLength(250)]
    public string PickupLocation { get; init; } = string.Empty;

    [Required]
    [MaxLength(250)]
    public string DropoffLocation { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal CommissionAmount { get; init; }

    [MaxLength(100)]
    public string? BolNumber { get; init; }

    [Range(0, double.MaxValue)]
    public decimal FuelCostAmount { get; init; }

    [Range(0, int.MaxValue)]
    public int WaitTimeMinutes { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }
}
