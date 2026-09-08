using TruckDriverTrips.Api.Dtos.Trips;

namespace TruckDriverTrips.Api.Services;

public interface ITripService
{
    Task<IReadOnlyList<TripResponse>> GetTripsAsync(
        TripQueryRequest query,
        string currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<TripResponse?> GetTripAsync(
        Guid id,
        string currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<TripResponse> CreateTripAsync(
        TripUpsertRequest request,
        string currentUserId,
        CancellationToken cancellationToken);

    Task<TripResponse?> UpdateTripAsync(
        Guid id,
        TripUpsertRequest request,
        string currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<bool> DeleteTripAsync(
        Guid id,
        string currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<TripSummaryResponse> GetSummaryAsync(
        TripQueryRequest query,
        string currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken);
}
