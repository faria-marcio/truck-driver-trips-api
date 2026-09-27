using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruckDriverTrips.Api.Dtos.Trips;
using TruckDriverTrips.Api.Infrastructure;
using TruckDriverTrips.Api.Services;

namespace TruckDriverTrips.Api.Controllers;

[ApiController]
[Route("api/trips")]
[Authorize(Roles = $"{IdentitySeed.DriverRole},{IdentitySeed.AdminRole}")]
public sealed class TripsController : ControllerBase
{
    private readonly ITripService _tripService;

    public TripsController(ITripService tripService)
    {
        _tripService = tripService;
    }

    [HttpGet]
    [ProducesResponseType<List<TripResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TripResponse>>> GetTrips(
        [FromQuery] TripQueryRequest query,
        CancellationToken cancellationToken)
    {
        if (!ValidateQuery(query, out var queryProblem))
        {
            return BadRequest(queryProblem);
        }

        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        var trips = await _tripService.GetTripsAsync(
            query,
            currentUserId,
            User.IsInRole(IdentitySeed.AdminRole),
            cancellationToken);

        return Ok(trips);
    }

    [HttpGet("summary")]
    [ProducesResponseType<TripSummaryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TripSummaryResponse>> GetSummary(
        [FromQuery] TripQueryRequest query,
        CancellationToken cancellationToken)
    {
        if (!ValidateQuery(query, out var queryProblem))
        {
            return BadRequest(queryProblem);
        }

        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        var summary = await _tripService.GetSummaryAsync(
            query,
            currentUserId,
            User.IsInRole(IdentitySeed.AdminRole),
            cancellationToken);

        return Ok(summary);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripResponse>> GetTrip(
        Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        var trip = await _tripService.GetTripAsync(
            id,
            currentUserId,
            User.IsInRole(IdentitySeed.AdminRole),
            cancellationToken);

        return trip is null ? NotFound() : Ok(trip);
    }

    [HttpPost]
    [ProducesResponseType<TripResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TripResponse>> CreateTrip(
        TripUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidateTrip(request, out var tripProblem))
        {
            return BadRequest(tripProblem);
        }

        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        var trip = await _tripService.CreateTripAsync(request, currentUserId, cancellationToken);
        return CreatedAtAction(nameof(GetTrip), new { id = trip.Id }, trip);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<TripResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripResponse>> UpdateTrip(
        Guid id,
        TripUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidateTrip(request, out var tripProblem))
        {
            return BadRequest(tripProblem);
        }

        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        try
        {
            var trip = await _tripService.UpdateTripAsync(
                id,
                request,
                currentUserId,
                User.IsInRole(IdentitySeed.AdminRole),
                cancellationToken);

            return trip is null ? NotFound() : Ok(trip);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(CreateConflictProblem());
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteTrip(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        try
        {
            var deleted = await _tripService.DeleteTripAsync(
                id,
                currentUserId,
                User.IsInRole(IdentitySeed.AdminRole),
                cancellationToken);

            return deleted ? NoContent() : NotFound();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(CreateConflictProblem());
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
    }

    private static bool ValidateQuery(TripQueryRequest query, out ProblemDetails problemDetails)
    {
        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
        {
            problemDetails = CreateBadRequestProblem("From must be on or before To.");
            return false;
        }

        problemDetails = null!;
        return true;
    }

    private static bool ValidateTrip(TripUpsertRequest request, out ProblemDetails problemDetails)
    {
        if (request.Date is null)
        {
            problemDetails = CreateBadRequestProblem("Date is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.TruckId))
        {
            problemDetails = CreateBadRequestProblem("TruckId is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.PickupLocation))
        {
            problemDetails = CreateBadRequestProblem("PickupLocation is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.DropoffLocation))
        {
            problemDetails = CreateBadRequestProblem("DropoffLocation is required.");
            return false;
        }

        if (request.EndKm <= request.StartKm)
        {
            problemDetails = CreateBadRequestProblem("EndKm must be greater than StartKm.");
            return false;
        }

        problemDetails = null!;
        return true;
    }

    private static ProblemDetails CreateBadRequestProblem(string title) => new()
    {
        Title = title,
        Status = StatusCodes.Status400BadRequest,
        Type = "https://httpstatuses.com/400"
    };

    private static ProblemDetails CreateConflictProblem() => new()
    {
        Title = "The trip was modified by another request.",
        Status = StatusCodes.Status409Conflict,
        Type = "https://httpstatuses.com/409"
    };
}
