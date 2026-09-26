namespace TruckDriverTrips.Api.Dtos.Trucks;

/// <summary>Represents a managed truck and its current assignment.</summary>
public sealed record TruckResponse(
    Guid Id,
    string RegistrationNumber,
    string? Make,
    string? Model,
    bool IsActive,
    DateTime? RetiredAtUtc,
    string? AssignedDriverId,
    string? AssignedDriverName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
