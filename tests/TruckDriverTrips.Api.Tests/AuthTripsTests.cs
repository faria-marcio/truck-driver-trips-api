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
        var updatePayload = new
        {
            date = "2026-08-21",
            startTime = "10:00:00",
            endTime = "11:00:00",
            distanceKm = 25.1m,
            pickupLocation = "Updated Start",
            dropoffLocation = "Updated End"
        };

        var otherUpdate = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/trips/{tripId}", otherDriver.Token, updatePayload);
        Assert.Equal(HttpStatusCode.NotFound, otherUpdate.StatusCode);

        var adminUpdate = await SendAuthorizedAsync(client, HttpMethod.Put, $"/api/trips/{tripId}", admin.Token, updatePayload);
        Assert.Equal(HttpStatusCode.OK, adminUpdate.StatusCode);

        var otherDelete = await SendAuthorizedAsync(client, HttpMethod.Delete, $"/api/trips/{tripId}", otherDriver.Token);
        Assert.Equal(HttpStatusCode.NotFound, otherDelete.StatusCode);

        var adminDelete = await SendAuthorizedAsync(client, HttpMethod.Delete, $"/api/trips/{tripId}", admin.Token);
        Assert.Equal(HttpStatusCode.NoContent, adminDelete.StatusCode);
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

    private static async Task<Guid> CreateTripAsync(HttpClient client, string token, string pickup, string dropoff)
    {
        var payload = new
        {
            date = "2026-08-20",
            startTime = "08:00:00",
            endTime = "09:00:00",
            distanceKm = 12.5m,
            pickupLocation = pickup,
            dropoffLocation = dropoff
        };

        var response = await SendAuthorizedAsync(client, HttpMethod.Post, "/api/trips", token, payload);
        response.EnsureSuccessStatusCode();

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        return Guid.Parse(json["id"]!.GetValue<string>());
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(HttpClient client, HttpMethod method, string url, string token, object? body = null)
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
