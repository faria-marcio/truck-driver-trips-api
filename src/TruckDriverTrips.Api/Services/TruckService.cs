using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TruckDriverTrips.Api.Data;
using TruckDriverTrips.Api.Dtos.Trucks;
using TruckDriverTrips.Api.Infrastructure;
using TruckDriverTrips.Api.Models;

namespace TruckDriverTrips.Api.Services;

public sealed class TruckService : ITruckService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public TruckService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<TruckResponse>> GetTrucksAsync(
        bool includeRetired,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var trucks = _dbContext.Trucks.AsNoTracking();
        if (!isAdmin || !includeRetired)
        {
            trucks = trucks.Where(x => x.IsActive);
        }

        return await trucks
            .OrderBy(x => x.RegistrationNumber)
            .Select(ToResponseExpression)
            .ToListAsync(cancellationToken);
    }

    public Task<TruckResponse?> GetTruckAsync(
        Guid id,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var trucks = _dbContext.Trucks.AsNoTracking();
        if (!isAdmin)
        {
            trucks = trucks.Where(x => x.IsActive);
        }

        return trucks
            .Where(x => x.Id == id)
            .Select(ToResponseExpression)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<TruckResponse> CreateTruckAsync(
        TruckCreateRequest request,
        CancellationToken cancellationToken)
    {
        var registrationNumber = NormalizeRegistrationNumber(request.RegistrationNumber);
        if (await _dbContext.Trucks.AnyAsync(
                x => x.RegistrationNumber == registrationNumber,
                cancellationToken))
        {
            throw new TruckConflictException(
                $"A truck with registration number '{registrationNumber}' already exists.");
        }

        var truck = new Truck
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = registrationNumber,
            Make = NormalizeOptional(request.Make),
            Model = NormalizeOptional(request.Model),
            IsActive = true
        };

        _dbContext.Trucks.Add(truck);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetTruckResponseAsync(truck.Id, cancellationToken)
            ?? throw new InvalidOperationException("The created truck could not be loaded.");
    }

    public async Task<TruckResponse?> UpdateTruckAsync(
        Guid id,
        TruckUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var truck = await _dbContext.Trucks
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (truck is null)
        {
            return null;
        }

        var registrationNumber = NormalizeRegistrationNumber(request.RegistrationNumber);
        if (await _dbContext.Trucks.AnyAsync(
                x => x.Id != id && x.RegistrationNumber == registrationNumber,
                cancellationToken))
        {
            throw new TruckConflictException(
                $"A truck with registration number '{registrationNumber}' already exists.");
        }

        truck.RegistrationNumber = registrationNumber;
        truck.Make = NormalizeOptional(request.Make);
        truck.Model = NormalizeOptional(request.Model);

        if (request.IsActive is false)
        {
            Retire(truck);
        }
        else if (request.IsActive is true)
        {
            truck.IsActive = true;
            truck.RetiredAtUtc = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetTruckResponseAsync(truck.Id, cancellationToken);
    }

    public async Task<TruckResponse?> RetireTruckAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var truck = await _dbContext.Trucks
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (truck is null)
        {
            return null;
        }

        if (truck.IsActive)
        {
            Retire(truck);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return await GetTruckResponseAsync(truck.Id, cancellationToken);
    }

    public Task<TruckResponse?> GetCurrentAssignmentAsync(
        string driverId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Trucks
            .AsNoTracking()
            .Where(x => x.AssignedDriverId == driverId && x.IsActive)
            .Select(ToResponseExpression)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<TruckResponse?> AssignTruckToDriverAsync(
        string driverId,
        Guid? truckId,
        CancellationToken cancellationToken)
    {
        var driver = await _userManager.FindByIdAsync(driverId);
        if (driver is null)
        {
            throw new DriverNotFoundException(driverId);
        }

        if (!await _userManager.IsInRoleAsync(driver, IdentitySeed.DriverRole))
        {
            throw new TruckConflictException("Only users with the Driver role may receive a truck assignment.");
        }

        var currentAssignment = await _dbContext.Trucks
            .SingleOrDefaultAsync(x => x.AssignedDriverId == driverId, cancellationToken);

        if (truckId is null)
        {
            if (currentAssignment is not null)
            {
                currentAssignment.AssignedDriverId = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return null;
        }

        var truck = await _dbContext.Trucks
            .SingleOrDefaultAsync(x => x.Id == truckId.Value, cancellationToken);
        if (truck is null)
        {
            throw new TruckNotFoundException(truckId.Value);
        }

        if (!truck.IsActive)
        {
            throw new TruckConflictException("A retired truck cannot be assigned.");
        }

        if (currentAssignment is not null && currentAssignment.Id != truck.Id)
        {
            currentAssignment.AssignedDriverId = null;
        }

        truck.AssignedDriverId = driverId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetTruckResponseAsync(truck.Id, cancellationToken);
    }

    public async Task<bool> ClearTruckAssignmentAsync(
        Guid truckId,
        CancellationToken cancellationToken)
    {
        var truck = await _dbContext.Trucks
            .SingleOrDefaultAsync(x => x.Id == truckId, cancellationToken);
        if (truck is null)
        {
            return false;
        }

        if (truck.AssignedDriverId is not null)
        {
            truck.AssignedDriverId = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public static string NormalizeRegistrationNumber(string value) =>
        value.Trim().ToUpperInvariant();

    private async Task<TruckResponse?> GetTruckResponseAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Trucks
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ToResponseExpression)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static void Retire(Truck truck)
    {
        truck.IsActive = false;
        truck.RetiredAtUtc = DateTime.UtcNow;
        truck.AssignedDriverId = null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static readonly Expression<Func<Truck, TruckResponse>> ToResponseExpression = x => new TruckResponse(
        x.Id,
        x.RegistrationNumber,
        x.Make,
        x.Model,
        x.IsActive,
        x.RetiredAtUtc,
        x.AssignedDriverId,
        x.AssignedDriver == null ? null : x.AssignedDriver.Name,
        x.CreatedAtUtc,
        x.UpdatedAtUtc);
}

public sealed class TruckNotFoundException : Exception
{
    public TruckNotFoundException(Guid truckId)
        : base($"Truck '{truckId}' was not found.")
    {
    }
}

public sealed class DriverNotFoundException : Exception
{
    public DriverNotFoundException(string driverId)
        : base($"Driver '{driverId}' was not found.")
    {
    }
}

public sealed class TruckConflictException : Exception
{
    public TruckConflictException(string message)
        : base(message)
    {
    }
}
