using Microsoft.AspNetCore.Identity;

namespace TruckDriverTrips.Api.Infrastructure;

public static class IdentitySeed
{
    public const string DriverRole = "Driver";
    public const string AdminRole = "Admin";

    public static async Task EnsureRolesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in new[] { DriverRole, AdminRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to create role '{role}'.");
                }
            }
        }
    }
}
