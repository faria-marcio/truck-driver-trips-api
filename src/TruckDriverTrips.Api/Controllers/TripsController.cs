using System.Security.Claims;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruckDriverTrips.Api.Data;
using TruckDriverTrips.Api.Dtos.Trips;
using TruckDriverTrips.Api.Infrastructure;
using TruckDriverTrips.Api.Models;

namespace TruckDriverTrips.Api.Controllers;

[ApiController]
[Route("api/trips")]
[Authorize(Roles = $"{IdentitySeed.DriverRole},{IdentitySeed.AdminRole}")]
public sealed class TripsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public TripsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [ProducesResponseType<List<TripResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TripResponse>>> GetTrips()
    {
        var isAdmin = User.IsInRole(IdentitySeed.AdminRole);
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        IQueryable<Trip> query = _dbContext.Trips.AsNoTracking();
        if (!isAdmin)
        {
            query = query.Where(x => x.DriverId == currentUserId);
        }

        var trips = await query
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.StartTime)
            .Select(ToResponseExpression)
            .ToListAsync();

        return Ok(trips);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripResponse>> GetTrip(Guid id)
    {
        var trip = await FindAuthorizedTripAsync(id);
        if (trip is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(trip));
    }

    [HttpPost]
    [ProducesResponseType<TripResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TripResponse>> CreateTrip(TripUpsertRequest request)
    {
        if (!ValidateTripWindow(request, out var errorMessage))
        {
            return BadRequest(new ProblemDetails
            {
                Title = errorMessage,
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            });
        }

        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            Date = request.Date!.Value,
            StartTime = request.StartTime!.Value,
            EndTime = request.EndTime!.Value,
            DistanceKm = request.DistanceKm,
            PickupLocation = request.PickupLocation.Trim(),
            DropoffLocation = request.DropoffLocation.Trim(),
            DriverId = currentUserId
        };

        _dbContext.Trips.Add(trip);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTrip), new { id = trip.Id }, ToResponse(trip));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TripResponse>> UpdateTrip(Guid id, TripUpsertRequest request)
    {
        if (!ValidateTripWindow(request, out var errorMessage))
        {
            return BadRequest(new ProblemDetails
            {
                Title = errorMessage,
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            });
        }

        var trip = await FindAuthorizedTripAsync(id);
        if (trip is null)
        {
            return NotFound();
        }

        trip.Date = request.Date!.Value;
        trip.StartTime = request.StartTime!.Value;
        trip.EndTime = request.EndTime!.Value;
        trip.DistanceKm = request.DistanceKm;
        trip.PickupLocation = request.PickupLocation.Trim();
        trip.DropoffLocation = request.DropoffLocation.Trim();

        await _dbContext.SaveChangesAsync();
        return Ok(ToResponse(trip));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTrip(Guid id)
    {
        var trip = await FindAuthorizedTripAsync(id);
        if (trip is null)
        {
            return NotFound();
        }

        _dbContext.Trips.Remove(trip);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private async Task<Trip?> FindAuthorizedTripAsync(Guid id)
    {
        var isAdmin = User.IsInRole(IdentitySeed.AdminRole);
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return null;
        }

        var trip = await _dbContext.Trips.FirstOrDefaultAsync(x => x.Id == id);
        if (trip is null)
        {
            return null;
        }

        if (isAdmin || trip.DriverId == currentUserId)
        {
            return trip;
        }

        return null;
    }

    private static readonly Expression<Func<Trip, TripResponse>> ToResponseExpression = x => new TripResponse(
        x.Id,
        x.Date,
        x.StartTime,
        x.EndTime,
        x.DistanceKm,
        x.PickupLocation,
        x.DropoffLocation,
        x.DriverId,
        x.CreatedAtUtc,
        x.UpdatedAtUtc);

    private static TripResponse ToResponse(Trip trip) => new(
        trip.Id,
        trip.Date,
        trip.StartTime,
        trip.EndTime,
        trip.DistanceKm,
        trip.PickupLocation,
        trip.DropoffLocation,
        trip.DriverId,
        trip.CreatedAtUtc,
        trip.UpdatedAtUtc);

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
    }

    private static bool ValidateTripWindow(TripUpsertRequest request, out string? errorMessage)
    {
        errorMessage = null;

        if (request.StartTime is null || request.EndTime is null || request.Date is null)
        {
            return true;
        }

        if (request.EndTime <= request.StartTime)
        {
            errorMessage = "EndTime must be greater than StartTime.";
            return false;
        }

        return true;
    }
}
