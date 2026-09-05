using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TruckDriverTrips.Api.Dtos.Auth;
using TruckDriverTrips.Api.Infrastructure;
using TruckDriverTrips.Api.Models;
using TruckDriverTrips.Api.Services;

namespace TruckDriverTrips.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is not null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Email already registered.",
                Status = StatusCodes.Status409Conflict,
                Type = "https://httpstatuses.com/409"
            });
        }

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            Name = request.Name.Trim()
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, IdentitySeed.DriverRole);
        if (!roleResult.Succeeded)
        {
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        var token = _jwtTokenService.CreateToken(user, IdentitySeed.DriverRole);
        var response = new AuthResponse(token, new AuthUserResponse(user.Id, user.Email ?? string.Empty, user.Name, IdentitySeed.DriverRole));

        return Created(string.Empty, response);
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid credentials.",
                Status = StatusCodes.Status401Unauthorized,
                Type = "https://httpstatuses.com/401"
            });
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid credentials.",
                Status = StatusCodes.Status401Unauthorized,
                Type = "https://httpstatuses.com/401"
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.Contains(IdentitySeed.AdminRole)
            ? IdentitySeed.AdminRole
            : roles.FirstOrDefault() ?? IdentitySeed.DriverRole;

        var token = _jwtTokenService.CreateToken(user, role);
        return Ok(new AuthResponse(token, new AuthUserResponse(user.Id, user.Email ?? string.Empty, user.Name, role)));
    }
}
