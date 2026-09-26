using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TruckDriverTrips.Api.Data;
using TruckDriverTrips.Api.Dtos.Trips;
using TruckDriverTrips.Api.Models;

namespace TruckDriverTrips.Api.Services;

public sealed class TripService : ITripService
{
    private const int DefaultPageSize = 50;
    private readonly ApplicationDbContext _dbContext;

    public TripService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TripResponse>> GetTripsAsync(
        TripQueryRequest query,
        string currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        IQueryable<Trip> tripsQuery = BuildAuthorizedQuery(query, currentUserId, isAdmin)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id);

        if (query.Page.HasValue || query.PageSize.HasValue)
        {
            var page = query.Page ?? 1;
            var pageSize = query.PageSize ?? DefaultPageSize;
            tripsQuery = tripsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
        }

        return await tripsQuery
            .Select(ToResponseExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<TripResponse?> GetTripAsync(
        Guid id,
        string currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var trip = await BuildAuthorizedQuery(
                new TripQueryRequest(),
                currentUserId,
                isAdmin)
            .Where(x => x.Id == id)
            .Select(ToResponseExpression)
            .FirstOrDefaultAsync(cancellationToken);

        return trip;
    }

    public async Task<TripResponse> CreateTripAsync(
        TripUpsertRequest request,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var truckId = await ResolveTruckIdAsync(
            request.TruckId,
            currentUserId,
            existingTruckId: null,
            cancellationToken: cancellationToken);

        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            DriverId = currentUserId,
            TruckId = truckId
        };

        ApplyEditableFields(trip, request, truckId);
        _dbContext.Trips.Add(trip);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetTripAsync(
                trip.Id,
                currentUserId,
                isAdmin: false,
                cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The created trip could not be loaded.");
    }

    public async Task<TripResponse?> UpdateTripAsync(
        Guid id,
        TripUpsertRequest request,
        string currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var trip = await BuildAuthorizedQuery(
                new TripQueryRequest(),
                currentUserId,
                isAdmin,
                trackEntities: true)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (trip is null)
        {
            return null;
        }

        var truckId = await ResolveTruckIdAsync(
            request.TruckId,
            currentUserId,
            trip.TruckId,
            cancellationToken);

        ApplyEditableFields(trip, request, truckId);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetTripAsync(id, currentUserId, isAdmin, cancellationToken);
    }

    public async Task<bool> DeleteTripAsync(
        Guid id,
        string currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var trip = await BuildAuthorizedQuery(
                new TripQueryRequest(),
                currentUserId,
                isAdmin,
                trackEntities: true)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (trip is null)
        {
            return false;
        }

        _dbContext.Trips.Remove(trip);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TripSummaryResponse> GetSummaryAsync(
        TripQueryRequest query,
        string currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var aggregate = await BuildAuthorizedQuery(query, currentUserId, isAdmin)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                TotalDistanceKm = group.Sum(x => x.DistanceKm),
                TotalCommissionAmount = group.Sum(x => x.CommissionAmount)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (aggregate is null)
        {
            return new TripSummaryResponse(0, 0, 0, 0);
        }

        var commissionPerKm = aggregate.TotalDistanceKm == 0
            ? 0
            : aggregate.TotalCommissionAmount / aggregate.TotalDistanceKm;

        return new TripSummaryResponse(
            aggregate.Count,
            aggregate.TotalDistanceKm,
            aggregate.TotalCommissionAmount,
            commissionPerKm);
    }

    private IQueryable<Trip> BuildAuthorizedQuery(
        TripQueryRequest query,
        string currentUserId,
        bool isAdmin,
        bool trackEntities = false)
    {
        IQueryable<Trip> trips = _dbContext.Trips;
        if (!trackEntities)
        {
            trips = trips.AsNoTracking();
        }

        if (!isAdmin)
        {
            trips = trips.Where(x => x.DriverId == currentUserId);
        }

        if (query.From.HasValue)
        {
            trips = trips.Where(x => x.Date >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            trips = trips.Where(x => x.Date <= query.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.TruckId))
        {
            var truckIdentifier = query.TruckId.Trim();
            if (Guid.TryParse(truckIdentifier, out var truckId))
            {
                trips = trips.Where(x => x.TruckId == truckId);
            }
            else
            {
                var registrationNumber = TruckService.NormalizeRegistrationNumber(truckIdentifier);
                trips = trips.Where(x => x.Truck!.RegistrationNumber == registrationNumber);
            }
        }

        return trips;
    }

    private async Task<Guid> ResolveTruckIdAsync(
        string? truckIdentifier,
        string currentUserId,
        Guid? existingTruckId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(truckIdentifier))
        {
            if (existingTruckId.HasValue)
            {
                return existingTruckId.Value;
            }

            var assignedTruckId = await _dbContext.Trucks
                .Where(x => x.AssignedDriverId == currentUserId && x.IsActive)
                .Select(x => (Guid?)x.Id)
                .SingleOrDefaultAsync(cancellationToken);

            return assignedTruckId
                ?? throw new TripValidationException(
                    "TruckId is required when the authenticated driver has no active truck assignment.");
        }

        var trimmedIdentifier = truckIdentifier.Trim();
        Truck? truck;
        if (Guid.TryParse(trimmedIdentifier, out var truckId))
        {
            truck = await _dbContext.Trucks
                .SingleOrDefaultAsync(x => x.Id == truckId, cancellationToken);
        }
        else
        {
            var registrationNumber = TruckService.NormalizeRegistrationNumber(trimmedIdentifier);
            truck = await _dbContext.Trucks
                .SingleOrDefaultAsync(
                    x => x.RegistrationNumber == registrationNumber,
                    cancellationToken);
        }

        if (truck is null)
        {
            throw new TripValidationException(
                $"TruckId '{trimmedIdentifier}' does not identify an existing truck.");
        }

        if (!truck.IsActive && truck.Id != existingTruckId)
        {
            throw new TripConflictException("A retired truck cannot be used for a new trip assignment.");
        }

        return truck.Id;
    }

    private static void ApplyEditableFields(
        Trip trip,
        TripUpsertRequest request,
        Guid truckId)
    {
        trip.Date = request.Date!.Value;
        trip.TruckId = truckId;
        trip.StartKm = request.StartKm;
        trip.EndKm = request.EndKm;
        trip.DistanceKm = request.EndKm - request.StartKm;
        trip.PickupLocation = request.PickupLocation.Trim();
        trip.DropoffLocation = request.DropoffLocation.Trim();
        trip.CommissionAmount = request.CommissionAmount;
        trip.BolNumber = string.IsNullOrWhiteSpace(request.BolNumber)
            ? null
            : request.BolNumber.Trim();
        trip.FuelCostAmount = request.FuelCostAmount;
        trip.WaitTimeMinutes = request.WaitTimeMinutes;
        trip.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? null
            : request.Notes.Trim();
    }

    private static readonly Expression<Func<Trip, TripResponse>> ToResponseExpression = x => new TripResponse(
        x.Id,
        x.Date,
        x.TruckId,
        x.Truck!.RegistrationNumber,
        x.StartKm,
        x.EndKm,
        x.DistanceKm,
        x.PickupLocation,
        x.DropoffLocation,
        x.CommissionAmount,
        x.BolNumber,
        x.FuelCostAmount,
        x.WaitTimeMinutes,
        x.Notes,
        x.DriverId,
        x.CreatedAtUtc,
        x.UpdatedAtUtc,
        x.Version);
}

public sealed class TripValidationException : Exception
{
    public TripValidationException(string message)
        : base(message)
    {
    }
}

public sealed class TripConflictException : Exception
{
    public TripConflictException(string message)
        : base(message)
    {
    }
}
