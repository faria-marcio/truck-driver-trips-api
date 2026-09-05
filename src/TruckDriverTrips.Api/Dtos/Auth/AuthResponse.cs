namespace TruckDriverTrips.Api.Dtos.Auth;

public sealed record AuthResponse(string Token, AuthUserResponse User);

public sealed record AuthUserResponse(string Id, string Email, string Name, string Role);
