using System.ComponentModel.DataAnnotations;

namespace TruckDriverTrips.Api.Dtos.Auth;

public sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;
}
