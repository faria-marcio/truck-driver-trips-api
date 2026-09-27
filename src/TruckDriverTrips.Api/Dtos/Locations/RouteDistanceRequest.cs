using System.ComponentModel.DataAnnotations;

namespace TruckDriverTrips.Api.Dtos.Locations;

/// <summary>Coordinates used to calculate a driving route between two selected cities.</summary>
public sealed record RouteDistanceRequest
{
    [Range(-90, 90)]
    public double OriginLatitude { get; init; }

    [Range(-180, 180)]
    public double OriginLongitude { get; init; }

    [Range(-90, 90)]
    public double DestinationLatitude { get; init; }

    [Range(-180, 180)]
    public double DestinationLongitude { get; init; }
}
