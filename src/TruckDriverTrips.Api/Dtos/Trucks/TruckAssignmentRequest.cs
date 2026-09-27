using System.ComponentModel.DataAnnotations;

namespace TruckDriverTrips.Api.Dtos.Trucks;

public sealed record TruckAssignmentRequest
{
    [Required]
    public string DriverId { get; init; } = string.Empty;
}
