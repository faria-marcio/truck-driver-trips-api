using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TruckDriverTrips.Api.Dtos.Locations;
using TruckDriverTrips.Api.Infrastructure;
using TruckDriverTrips.Api.Options;
using TruckDriverTrips.Api.Services;

namespace TruckDriverTrips.Api.Controllers;

[ApiController]
[Route("api/locations")]
[Authorize(Roles = $"{IdentitySeed.DriverRole},{IdentitySeed.AdminRole}")]
public sealed class LocationsController(
    ICitySearchService citySearchService,
    IOptions<GeoapifyOptions> geoapifyOptions) : ControllerBase
{
    private readonly ICitySearchService _citySearchService = citySearchService;
    private readonly GeoapifyOptions _geoapifyOptions = geoapifyOptions.Value;

    [HttpGet("cities")]
    [ProducesResponseType<IReadOnlyList<CitySearchResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<CitySearchResponse>>> SearchCities(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        if (query.Trim().Length < 2)
        {
            return BadRequest(CreateProblem("Enter at least two characters to search for a city."));
        }

        if (query.Length > 100)
        {
            return BadRequest(CreateProblem("City searches cannot exceed 100 characters."));
        }

        if (!_geoapifyOptions.IsConfigured)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblem("City search is not configured.", StatusCodes.Status503ServiceUnavailable));
        }

        var response = await _citySearchService.SearchCitiesAsync(query, cancellationToken);

        return Ok(response);
    }

    [HttpPost("route")]
    [ProducesResponseType<RouteDistanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RouteDistanceResponse>> GetRouteDistance(
        RouteDistanceRequest request,
        CancellationToken cancellationToken)
    {
        if (!_geoapifyOptions.IsConfigured)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblem("Route calculation is not configured.", StatusCodes.Status503ServiceUnavailable));
        }

        var route = await _citySearchService.GetRouteDistanceAsync(request, cancellationToken);
        return route is null
            ? NotFound(CreateProblem("No driving route was found between the selected cities.", StatusCodes.Status404NotFound))
            : Ok(route);
    }

    private static ProblemDetails CreateProblem(string title, int status = StatusCodes.Status400BadRequest) => new()
    {
        Title = title,
        Status = status,
        Type = $"https://httpstatuses.com/{status}"
    };
}