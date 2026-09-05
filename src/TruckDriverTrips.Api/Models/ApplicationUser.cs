using Microsoft.AspNetCore.Identity;

namespace TruckDriverTrips.Api.Models;

public sealed class ApplicationUser : IdentityUser
{
    public string Name { get; set; } = string.Empty;
}
