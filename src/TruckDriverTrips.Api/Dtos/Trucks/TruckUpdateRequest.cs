using System.ComponentModel.DataAnnotations;

namespace TruckDriverTrips.Api.Dtos.Trucks;

public sealed record TruckUpdateRequest
{
    [Required, MaxLength(50)]
    public string RegistrationNumber { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? Make { get; init; }

    [MaxLength(100)]
    public string? Model { get; init; }

    /// <summary>When supplied, true restores a retired truck and false retires it.</summary>
    public bool? IsActive { get; init; }
}
