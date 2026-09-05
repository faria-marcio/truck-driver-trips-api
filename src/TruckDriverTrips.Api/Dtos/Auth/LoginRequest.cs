using System.ComponentModel.DataAnnotations;

namespace TruckDriverTrips.Api.Dtos.Auth;

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}
