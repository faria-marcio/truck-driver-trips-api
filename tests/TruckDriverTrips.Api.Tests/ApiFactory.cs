using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TruckDriverTrips.Api.Data;
using TruckDriverTrips.Api.Infrastructure;
using TruckDriverTrips.Api.Models;

namespace TruckDriverTrips.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var dbOptionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (dbOptionsDescriptor is not null)
            {
                services.Remove(dbOptionsDescriptor);
            }

            _connection.Open();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task PromoteToAdminAsync(string userId)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("User not found.");
        await userManager.AddToRoleAsync(user, IdentitySeed.AdminRole);
    }

    public async Task<Guid> EnsureActiveTruckAsync(string registrationNumber)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var normalizedRegistrationNumber = registrationNumber.Trim().ToUpperInvariant();
        var truck = await dbContext.Trucks
            .SingleOrDefaultAsync(x => x.RegistrationNumber == normalizedRegistrationNumber);

        if (truck is null)
        {
            truck = new Truck
            {
                Id = Guid.NewGuid(),
                RegistrationNumber = normalizedRegistrationNumber,
                IsActive = true
            };
            dbContext.Trucks.Add(truck);
            await dbContext.SaveChangesAsync();
        }

        return truck.Id;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
