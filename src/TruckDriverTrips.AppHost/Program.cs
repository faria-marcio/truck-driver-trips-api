var builder = DistributedApplication.CreateBuilder(args);

var jwtKey = builder.AddParameter("jwt-key", secret: true);
var nextAuthSecret = builder.AddParameter("nextauth-secret", secret: true);
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();
var database = postgres.AddDatabase("tripsdb");

var api = builder.AddProject<Projects.TruckDriverTrips_Api>("api")
    .WithExternalHttpEndpoints()
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithReference(database)
    .WaitFor(database);

var frontendPath = FindFrontendPath(builder.AppHostDirectory);

var frontend = builder.AddNextJsApp("frontend", frontendPath)
    .WithEnvironment("NEXT_PUBLIC_API_URL", api.GetEndpoint("https"))
    .WithEnvironment("API_URL", api.GetEndpoint("https"))
    .WithEnvironment("NEXTAUTH_SECRET", nextAuthSecret)
    .WithReference(api)
    .WaitFor(api);

frontend.WithEnvironment("NEXTAUTH_URL", frontend.GetEndpoint("http"));
api.WithEnvironment("Cors__AllowedOrigins__0", frontend.GetEndpoint("http"));

await builder.Build().RunAsync();

static string FindFrontendPath(string appHostDirectory)
{
    var candidates = new List<string>();
    var current = new DirectoryInfo(appHostDirectory);

    while (current is not null)
    {
        var candidate = Path.Combine(current.FullName, "truck-driver-trips-web");
        if (Directory.Exists(candidate)) candidates.Add(candidate);

        current = current.Parent;
    }

    var preferredCandidate = candidates.FirstOrDefault(candidate =>
        string.Equals(
            Directory.GetParent(candidate)?.Name,
            "repos",
            StringComparison.OrdinalIgnoreCase));

    if (preferredCandidate is not null) return preferredCandidate;

    if (candidates.Count > 0) return candidates[0];

    throw new DirectoryNotFoundException(
        $"Could not find the sibling frontend repository 'truck-driver-trips-web' while searching from '{appHostDirectory}'. " +
        "Place it beside the repository root or update the AppHost path resolution.");
}