namespace TruckDriverTrips.Api.Models;

public sealed class Trip
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string TruckId { get; set; } = string.Empty;
    public decimal StartKm { get; set; }
    public decimal EndKm { get; set; }
    public decimal DistanceKm { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public decimal CommissionAmount { get; set; }
    public string? BolNumber { get; set; }
    public decimal FuelCostAmount { get; set; }
    public int WaitTimeMinutes { get; set; }
    public string? Notes { get; set; }
    public string DriverId { get; set; } = string.Empty;
    public ApplicationUser? Driver { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}
