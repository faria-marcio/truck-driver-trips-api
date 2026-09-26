using TruckDriverTrips.Api.Dtos.Trucks;

namespace TruckDriverTrips.Api.Services;

public interface ITruckService
{
    Task<IReadOnlyList<TruckResponse>> GetTrucksAsync(
        bool includeRetired,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<TruckResponse?> GetTruckAsync(
        Guid id,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<TruckResponse> CreateTruckAsync(
        TruckCreateRequest request,
        CancellationToken cancellationToken);

    Task<TruckResponse?> UpdateTruckAsync(
        Guid id,
        TruckUpdateRequest request,
        CancellationToken cancellationToken);

    Task<TruckResponse?> RetireTruckAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<TruckResponse?> GetCurrentAssignmentAsync(
        string driverId,
        CancellationToken cancellationToken);

    Task<TruckResponse?> AssignTruckToDriverAsync(
        string driverId,
        Guid? truckId,
        CancellationToken cancellationToken);

    Task<bool> ClearTruckAssignmentAsync(
        Guid truckId,
        CancellationToken cancellationToken);
}
