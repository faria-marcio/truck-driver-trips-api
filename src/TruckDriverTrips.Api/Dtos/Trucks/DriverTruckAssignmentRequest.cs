namespace TruckDriverTrips.Api.Dtos.Trucks;

public sealed record DriverTruckAssignmentRequest
{
    public Guid? TruckId { get; init; }
}
