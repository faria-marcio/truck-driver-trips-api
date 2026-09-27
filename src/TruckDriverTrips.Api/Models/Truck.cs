namespace TruckDriverTrips.Api.Models;

public sealed class Truck
{
    public Guid Id { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? RetiredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? AssignedDriverId { get; set; }
    public ApplicationUser? AssignedDriver { get; set; }
    public ICollection<Trip> Trips { get; } = new List<Trip>();
}
