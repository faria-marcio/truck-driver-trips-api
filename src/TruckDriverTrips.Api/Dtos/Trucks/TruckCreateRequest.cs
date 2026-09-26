using System.ComponentModel.DataAnnotations;

namespace TruckDriverTrips.Api.Dtos.Trucks;

public sealed record TruckCreateRequest
{
    [Required, MaxLength(50)]
    public string RegistrationNumber { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? Make { get; init; }

    [MaxLength(100)]
    public string? Model { get; init; }
}
