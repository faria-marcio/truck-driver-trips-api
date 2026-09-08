using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TruckDriverTrips.Api.Data;
using TruckDriverTrips.Api.Dtos.Trips;
using TruckDriverTrips.Api.Models;

namespace TruckDriverTrips.Api.Services;

public sealed class TripService(ApplicationDbContext dbContext) : ITripService
{
    private const int DefaultPageSize = 50;
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<TripResponse>> GetTripsAsync(TripQueryRequest query, string currentUserId, bool isAdmin, CancellationToken cancellationToken)
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

    public async Task<TripResponse?> GetTripAsync(Guid id, string currentUserId, bool isAdmin, CancellationToken cancellationToken)
    {
        var trip = await BuildAuthorizedQuery(
                new TripQueryRequest(),
                currentUserId,
                isAdmin)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return trip is null ? null : ToResponse(trip);
    }

    public async Task<TripResponse> CreateTripAsync(TripUpsertRequest request, string currentUserId, CancellationToken cancellationToken)
    {
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            DriverId = currentUserId
        };

        ApplyEditableFields(trip, request);
        _dbContext.Trips.Add(trip);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(trip);
    }

    public async Task<TripResponse?> UpdateTripAsync(Guid id, TripUpsertRequest request, string currentUserId, bool isAdmin, CancellationToken cancellationToken)
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

        ApplyEditableFields(trip, request);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(trip);
    }

    public async Task<bool> DeleteTripAsync(Guid id, string currentUserId, bool isAdmin, CancellationToken cancellationToken)
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

    public async Task<TripSummaryResponse> GetSummaryAsync(TripQueryRequest query, string currentUserId, bool isAdmin, CancellationToken cancellationToken)
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

    private IQueryable<Trip> BuildAuthorizedQuery(TripQueryRequest query, string currentUserId, bool isAdmin, bool trackEntities = false)
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
            var truckId = query.TruckId.Trim();
            trips = trips.Where(x => x.TruckId == truckId);
        }

        return trips;
    }

    private static void ApplyEditableFields(Trip trip, TripUpsertRequest request)
    {
        trip.Date = request.Date!.Value;
        trip.TruckId = request.TruckId.Trim();
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

    private static TripResponse ToResponse(Trip trip) => new(
        trip.Id,
        trip.Date,
        trip.TruckId,
        trip.StartKm,
        trip.EndKm,
        trip.DistanceKm,
        trip.PickupLocation,
        trip.DropoffLocation,
        trip.CommissionAmount,
        trip.BolNumber,
        trip.FuelCostAmount,
        trip.WaitTimeMinutes,
        trip.Notes,
        trip.DriverId,
        trip.CreatedAtUtc,
        trip.UpdatedAtUtc,
        trip.Version);
}