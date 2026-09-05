using TruckDriverTrips.Api.Models;

namespace TruckDriverTrips.Api.Services;

public interface IJwtTokenService
{
    string CreateToken(ApplicationUser user, string role);
}
