namespace TruckDriverTrips.Api.Options;

public sealed class GeoapifyOptions
{
    public const string SectionName = "Geoapify";

    public string? ApiKey { get; init; }

    public string CountryCode { get; init; } = "br";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
