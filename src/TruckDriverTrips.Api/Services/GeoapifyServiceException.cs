using System.Net;

namespace TruckDriverTrips.Api.Services;

public sealed class GeoapifyServiceException : Exception
{
    public GeoapifyServiceException(HttpStatusCode statusCode, string responseBody)
        : base($"Geoapify returned {(int)statusCode} ({statusCode}): {responseBody}")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string ResponseBody { get; }
}
