using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruckDriverTrips.Api.Dtos.Trucks;
using TruckDriverTrips.Api.Infrastructure;
using TruckDriverTrips.Api.Services;

namespace TruckDriverTrips.Api.Controllers;

[ApiController]
[Route("api/trucks")]
[Authorize(Roles = $"{IdentitySeed.DriverRole},{IdentitySeed.AdminRole}")]
public sealed class TrucksController : ControllerBase
{
    private readonly ITruckService _truckService;

    public TrucksController(ITruckService truckService)
    {
        _truckService = truckService;
    }

    [HttpGet]
    [ProducesResponseType<List<TruckResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TruckResponse>>> GetTrucks(
        [FromQuery] bool includeRetired,
        CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole(IdentitySeed.AdminRole);
        if (includeRetired && !isAdmin)
        {
            return Forbid();
        }

        return Ok(await _truckService.GetTrucksAsync(includeRetired, isAdmin, cancellationToken));
    }

    [HttpGet("me")]
    [HttpGet("my-assignment")]
    [HttpGet("/api/drivers/me/truck")]
    [ProducesResponseType<TruckResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TruckResponse>> GetCurrentAssignment(
        CancellationToken cancellationToken)
    {
        var driverId = GetCurrentUserId();
        if (driverId is null)
        {
            return Unauthorized();
        }

        var truck = await _truckService.GetCurrentAssignmentAsync(driverId, cancellationToken);
        return truck is null ? NotFound() : Ok(truck);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TruckResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TruckResponse>> GetTruck(
        Guid id,
        CancellationToken cancellationToken)
    {
        var truck = await _truckService.GetTruckAsync(
            id,
            User.IsInRole(IdentitySeed.AdminRole),
            cancellationToken);

        return truck is null ? NotFound() : Ok(truck);
    }

    [HttpPost]
    [Authorize(Roles = IdentitySeed.AdminRole)]
    [ProducesResponseType<TruckResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TruckResponse>> CreateTruck(
        TruckCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidateRegistrationNumber(request.RegistrationNumber, out var problem))
        {
            return BadRequest(problem);
        }

        try
        {
            var truck = await _truckService.CreateTruckAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetTruck), new { id = truck.Id }, truck);
        }
        catch (TruckConflictException exception)
        {
            return Conflict(CreateConflictProblem(exception.Message));
        }
        catch (DbUpdateException)
        {
            return Conflict(CreateConflictProblem("A truck with the same registration number already exists."));
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = IdentitySeed.AdminRole)]
    [ProducesResponseType<TruckResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TruckResponse>> UpdateTruck(
        Guid id,
        TruckUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidateRegistrationNumber(request.RegistrationNumber, out var problem))
        {
            return BadRequest(problem);
        }

        try
        {
            var truck = await _truckService.UpdateTruckAsync(id, request, cancellationToken);
            return truck is null ? NotFound() : Ok(truck);
        }
        catch (TruckConflictException exception)
        {
            return Conflict(CreateConflictProblem(exception.Message));
        }
        catch (DbUpdateException)
        {
            return Conflict(CreateConflictProblem("A truck with the same registration number already exists."));
        }
    }

    [HttpPost("{id:guid}/retire")]
    [Authorize(Roles = IdentitySeed.AdminRole)]
    [ProducesResponseType<TruckResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TruckResponse>> RetireTruck(
        Guid id,
        CancellationToken cancellationToken)
    {
        var truck = await _truckService.RetireTruckAsync(id, cancellationToken);
        return truck is null ? NotFound() : Ok(truck);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = IdentitySeed.AdminRole)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTruck(
        Guid id,
        CancellationToken cancellationToken)
    {
        var truck = await _truckService.RetireTruckAsync(id, cancellationToken);
        return truck is null ? NotFound() : NoContent();
    }

    [HttpPut("{truckId:guid}/assignment")]
    [Authorize(Roles = IdentitySeed.AdminRole)]
    [ProducesResponseType<TruckResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TruckResponse>> AssignTruck(
        Guid truckId,
        TruckAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var truck = await _truckService.AssignTruckToDriverAsync(
                request.DriverId.Trim(),
                truckId,
                cancellationToken);

            return truck is null ? NotFound() : Ok(truck);
        }
        catch (DriverNotFoundException exception)
        {
            return NotFound(CreateNotFoundProblem(exception.Message));
        }
        catch (TruckNotFoundException exception)
        {
            return NotFound(CreateNotFoundProblem(exception.Message));
        }
        catch (TruckConflictException exception)
        {
            return Conflict(CreateConflictProblem(exception.Message));
        }
        catch (DbUpdateException)
        {
            return Conflict(CreateConflictProblem("The truck assignment conflicts with another current assignment."));
        }
    }

    [HttpDelete("{truckId:guid}/assignment")]
    [Authorize(Roles = IdentitySeed.AdminRole)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearTruckAssignment(
        Guid truckId,
        CancellationToken cancellationToken)
    {
        var cleared = await _truckService.ClearTruckAssignmentAsync(truckId, cancellationToken);
        return cleared ? NoContent() : NotFound();
    }

    [HttpPut("assignments/{driverId}")]
    [HttpPut("/api/drivers/{driverId}/truck")]
    [Authorize(Roles = IdentitySeed.AdminRole)]
    [ProducesResponseType<TruckResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TruckResponse>> AssignTruckToDriver(
        string driverId,
        DriverTruckAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var truck = await _truckService.AssignTruckToDriverAsync(
                driverId.Trim(),
                request.TruckId,
                cancellationToken);

            return truck is null ? NoContent() : Ok(truck);
        }
        catch (DriverNotFoundException exception)
        {
            return NotFound(CreateNotFoundProblem(exception.Message));
        }
        catch (TruckNotFoundException exception)
        {
            return NotFound(CreateNotFoundProblem(exception.Message));
        }
        catch (TruckConflictException exception)
        {
            return Conflict(CreateConflictProblem(exception.Message));
        }
        catch (DbUpdateException)
        {
            return Conflict(CreateConflictProblem("The truck assignment conflicts with another current assignment."));
        }
    }

    [HttpDelete("assignments/{driverId}")]
    [HttpDelete("/api/drivers/{driverId}/truck")]
    [Authorize(Roles = IdentitySeed.AdminRole)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearDriverAssignment(
        string driverId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _truckService.AssignTruckToDriverAsync(
                driverId.Trim(),
                null,
                cancellationToken);

            return NoContent();
        }
        catch (DriverNotFoundException exception)
        {
            return NotFound(CreateNotFoundProblem(exception.Message));
        }
    }

    private string? GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub");

    private static bool ValidateRegistrationNumber(
        string registrationNumber,
        out ProblemDetails problemDetails)
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            problemDetails = new ProblemDetails
            {
                Title = "RegistrationNumber is required.",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            };
            return false;
        }

        problemDetails = null!;
        return true;
    }

    private static ProblemDetails CreateConflictProblem(string title) => new()
    {
        Title = title,
        Status = StatusCodes.Status409Conflict,
        Type = "https://httpstatuses.com/409"
    };

    private static ProblemDetails CreateNotFoundProblem(string title) => new()
    {
        Title = title,
        Status = StatusCodes.Status404NotFound,
        Type = "https://httpstatuses.com/404"
    };
}
