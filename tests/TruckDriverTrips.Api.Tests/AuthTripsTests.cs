using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace TruckDriverTrips.Api.Tests;

public sealed class AuthTripsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthTripsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_And_Login_ReturnsExpectedShape()
    {
        var client = _factory.CreateClient();
        var request = new { email = "driver@example.com", password = "Passw0rd1", name = "Driver One" };

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var json = JsonNode.Parse(await registerResponse.Content.ReadAsStringAsync())!.AsObject();
        Assert.False(string.IsNullOrWhiteSpace(json["token"]?.GetValue<string>()));
        Assert.Equal("driver@example.com", json["user"]!["email"]!.GetValue<string>());
        Assert.Equal("Driver", json["user"]!["role"]!.GetValue<string>());

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email = "driver@example.com", password = "Passw0rd1" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var request = new { email = "duplicate@example.com", password = "Passw0rd1", name = "Driver One" };

        var first = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { email = "invalid-login@example.com", password = "Passw0rd1", name = "Driver One" });

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "invalid-login@example.com", password = "WrongPass123" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Driver_CanOnlySeeOwnTrips()
    {
        var client = _factory.CreateClient();

        var user1 = await RegisterAndLoginAsync(client, "driver1@example.com", "Passw0rd1", "Driver 1");
        var user2 = await RegisterAndLoginAsync(client, "driver2@example.com", "Passw0rd1", "Driver 2");

        await CreateTripAsync(client, user1.Token, "A", "B");
        var otherTrip = await CreateTripAsync(client, user2.Token, "C", "D");

        var getResponse = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/trips", user1.Token);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var trips = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())!.AsArray();
        Assert.Single(trips);
        Assert.Equal(user1.UserId, trips[0]!["driverId"]!.GetValue<string>());

        var unauthorizedTripResponse = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/trips/{otherTrip}", user1.Token);
        Assert.Equal(HttpStatusCode.NotFound, unauthorizedTripResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_CanListAllTrips()
    {
        var client = _factory.CreateClient();

        var admin = await RegisterAndLoginAsync(client, "admin@example.com", "Passw0rd1", "Admin");
        await _factory.PromoteToAdminAsync(admin.UserId);
        admin = await LoginAsync(client, "admin@example.com", "Passw0rd1");

        var driver = await RegisterAndLoginAsync(client, "driver3@example.com", "Passw0rd1", "Driver 3");
        await CreateTripAsync(client, driver.Token, "X", "Y");

        var response = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/trips", admin.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var trips = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
        Assert.NotEmpty(trips);
        Assert.Contains(trips, x => x!["driverId"]!.GetValue<string>() == driver.UserId);
    }

    [Fact]
    public async Task Ownership_IsEnforced_ForUpdateAndDelete_WithAdminOverride()
    {
        var client = _factory.CreateClient();

        var owner = await RegisterAndLoginAsync(client, "owner@example.com", "Passw0rd1", "Owner");
        var otherDriver = await RegisterAndLoginAsync(client, "other@example.com", "Passw0rd1", "Other");
        var admin = await RegisterAndLoginAsync(client, "admin2@example.com", "Passw0rd1", "Admin 2");
        await _factory.PromoteToAdminAsync(admin.UserId);
        admin = await LoginAsync(client, "admin2@example.com", "Passw0rd1");

        var tripId = await CreateTripAsync(client, owner.Token, "Start", "End");
        var updatePayload = TripPayload(
            "Updated Start",
            "Updated End",
            truckId: "TRUCK-UPDATED",
            startKm: 200,
            endKm: 225,
            commissionAmount: 250);

        var otherUpdate = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/trips/{tripId}", otherDriver.Token, updatePayload);
        Assert.Equal(HttpStatusCode.NotFound, otherUpdate.StatusCode);

        var adminUpdate = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/trips/{tripId}", admin.Token, updatePayload);
        Assert.Equal(HttpStatusCode.OK, adminUpdate.StatusCode);
        var updatedTrip = JsonNode.Parse(await adminUpdate.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(25m, updatedTrip["distanceKm"]!.GetValue<decimal>());
        Assert.Equal(owner.UserId, updatedTrip["driverId"]!.GetValue<string>());
        Assert.Equal(2, updatedTrip["version"]!.GetValue<int>());

        var otherDelete = await SendAuthorizedAsync(client, HttpMethod.Delete, $"/api/trips/{tripId}", otherDriver.Token);
        Assert.Equal(HttpStatusCode.NotFound, otherDelete.StatusCode);

        var adminDelete = await SendAuthorizedAsync(client, HttpMethod.Delete, $"/api/trips/{tripId}", admin.Token);
        Assert.Equal(HttpStatusCode.NoContent, adminDelete.StatusCode);
    }

    [Fact]
    public async Task CreateTrip_WithInvalidDistance_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var user = await RegisterAndLoginAsync(client, "invalid-distance@example.com", "Passw0rd1", "Driver");

        var payload = TripPayload("A", "B", startKm: 12, endKm: 12);
        var response = await SendAuthorizedAsync(client, HttpMethod.Post, "/api/trips", user.Token, payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTrip_DerivesDistance_AndIgnoresProtectedFields()
    {
        var client = _factory.CreateClient();
        var user = await RegisterAndLoginAsync(client, "derived-distance@example.com", "Passw0rd1", "Driver");

        var payload = new
        {
            date = "2026-08-20",
            truckId = "TRUCK-DERIVED",
            startKm = 100m,
            endKm = 125m,
            distanceKm = 999m,
            pickupLocation = "A",
            dropoffLocation = "B",
            commissionAmount = 125m,
            bolNumber = "BOL-DERIVED",
            fuelCostAmount = 30m,
            waitTimeMinutes = 15,
            notes = "Test note",
            id = Guid.NewGuid(),
            driverId = "forged-driver",
            version = 99
        };

        var response = await SendAuthorizedAsync(client, HttpMethod.Post, "/api/trips", user.Token, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var trip = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(25m, trip["distanceKm"]!.GetValue<decimal>());
        Assert.Equal(user.UserId, trip["driverId"]!.GetValue<string>());
        Assert.Equal(1, trip["version"]!.GetValue<int>());
        Assert.Null(trip["startTime"]);
        Assert.Null(trip["endTime"]);
    }

    [Fact]
    public async Task Trips_CanFilter_AndSummaryUsesWeightedCommissionPerKm()
    {
        var client = _factory.CreateClient();
        var user = await RegisterAndLoginAsync(client, "filter-summary@example.com", "Passw0rd1", "Driver");

        await CreateTripAsync(
            client,
            user.Token,
            "A",
            "B",
            truckId: "TRUCK-A",
            date: "2026-08-20",
            startKm: 100,
            endKm: 110,
            commissionAmount: 100);
        await CreateTripAsync(
            client,
            user.Token,
            "C",
            "D",
            truckId: "TRUCK-A",
            date: "2026-08-21",
            startKm: 200,
            endKm: 220,
            commissionAmount: 300);
        await CreateTripAsync(
            client,
            user.Token,
            "E",
            "F",
            truckId: "TRUCK-B",
            date: "2026-08-22",
            startKm: 300,
            endKm: 350,
            commissionAmount: 500);

        var filteredResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/trips?from=2026-08-20&to=2026-08-21&truckId=TRUCK-A&page=1&pageSize=1",
            user.Token);
        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        var filteredTrips = JsonNode.Parse(await filteredResponse.Content.ReadAsStringAsync())!.AsArray();
        Assert.Single(filteredTrips);

        var summaryResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/trips/summary?from=2026-08-20&to=2026-08-21&truckId=TRUCK-A",
            user.Token);
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        var summary = JsonNode.Parse(await summaryResponse.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(2, summary["count"]!.GetValue<int>());
        Assert.Equal(30m, summary["totalDistanceKm"]!.GetValue<decimal>());
        Assert.Equal(400m, summary["totalCommissionAmount"]!.GetValue<decimal>());
        Assert.Equal(400m / 30m, summary["commissionPerKm"]!.GetValue<decimal>());
    }

    private static async Task<(string Token, string UserId)> RegisterAndLoginAsync(HttpClient client, string email, string password, string name)
    {
        await client.PostAsJsonAsync("/api/auth/register", new { email, password, name });
        return await LoginAsync(client, email, password);
    }

    private static async Task<(string Token, string UserId)> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        return (payload["token"]!.GetValue<string>(), payload["user"]!["id"]!.GetValue<string>());
    }

    private static async Task<Guid> CreateTripAsync(
        HttpClient client,
        string token,
        string pickup,
        string dropoff,
        string truckId = "TRUCK-001",
        string date = "2026-08-20",
        decimal startKm = 100,
        decimal endKm = 112.5m,
        decimal commissionAmount = 125)
    {
        var response = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/api/trips",
            token,
            TripPayload(pickup, dropoff, truckId, date, startKm, endKm, commissionAmount));
        response.EnsureSuccessStatusCode();

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        return Guid.Parse(json["id"]!.GetValue<string>());
    }

    private static object TripPayload(
        string pickup,
        string dropoff,
        string truckId = "TRUCK-001",
        string date = "2026-08-20",
        decimal startKm = 100,
        decimal endKm = 112.5m,
        decimal commissionAmount = 125)
    {
        return new
        {
            date,
            truckId,
            startKm,
            endKm,
            pickupLocation = pickup,
            dropoffLocation = dropoff,
            commissionAmount,
            bolNumber = "BOL-001",
            fuelCostAmount = 30m,
            waitTimeMinutes = 15,
            notes = "Test trip"
        };
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string token,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }
}
