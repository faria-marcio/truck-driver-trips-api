using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace TruckDriverTrips.Api.Tests;

public sealed class TruckManagementTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TruckManagementTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminCanManageAssignmentAndRetirement_WhileDriverReadsAndUsesCurrentTruck()
    {
        var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var admin = await RegisterAndLoginAsync(
            client,
            $"truck-admin-{suffix}@example.com",
            "Passw0rd1",
            "Truck Admin");
        await _factory.PromoteToAdminAsync(admin.UserId);
        admin = await LoginAsync(client, $"truck-admin-{suffix}@example.com", "Passw0rd1");

        var driver = await RegisterAndLoginAsync(
            client,
            $"truck-driver-{suffix}@example.com",
            "Passw0rd1",
            "Truck Driver");
        var registrationNumber = $"TRUCK-{suffix}";

        var createResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/api/trucks",
            admin.Token,
            new { registrationNumber, make = "Volvo", model = "FH" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var truck = JsonNode.Parse(await createResponse.Content.ReadAsStringAsync())!.AsObject();
        var truckId = Guid.Parse(truck["id"]!.GetValue<string>());
        Assert.Equal(registrationNumber.ToUpperInvariant(), truck["registrationNumber"]!.GetValue<string>());
        Assert.True(truck["isActive"]!.GetValue<bool>());

        var duplicateResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/api/trucks",
            admin.Token,
            new { registrationNumber = registrationNumber.ToLowerInvariant() });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var driverCreateResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/api/trucks",
            driver.Token,
            new { registrationNumber = $"FORBIDDEN-{suffix}" });
        Assert.Equal(HttpStatusCode.Forbidden, driverCreateResponse.StatusCode);

        var updateResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            $"/api/trucks/{truckId}",
            admin.Token,
            new { registrationNumber, make = "Volvo", model = "FH16" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatedTruck = JsonNode.Parse(await updateResponse.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal("FH16", updatedTruck["model"]!.GetValue<string>());

        var assignmentResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            $"/api/trucks/assignments/{driver.UserId}",
            admin.Token,
            new { truckId });
        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);
        Assert.Equal(driver.UserId, JsonNode.Parse(await assignmentResponse.Content.ReadAsStringAsync())!["assignedDriverId"]!.GetValue<string>());

        var currentAssignmentResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/trucks/me",
            driver.Token);
        Assert.Equal(HttpStatusCode.OK, currentAssignmentResponse.StatusCode);
        var currentAssignment = JsonNode.Parse(await currentAssignmentResponse.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(truckId, Guid.Parse(currentAssignment["id"]!.GetValue<string>()));

        var tripResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/api/trips",
            driver.Token,
            new
            {
                date = "2026-09-26",
                startKm = 100m,
                endKm = 125m,
                pickupLocation = "A",
                dropoffLocation = "B",
                commissionAmount = 125m
            });
        Assert.Equal(HttpStatusCode.Created, tripResponse.StatusCode);
        var trip = JsonNode.Parse(await tripResponse.Content.ReadAsStringAsync())!.AsObject();
        var tripId = Guid.Parse(trip["id"]!.GetValue<string>());
        Assert.Equal(truckId, Guid.Parse(trip["truckId"]!.GetValue<string>()));

        var retireResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            $"/api/trucks/{truckId}/retire",
            admin.Token);
        Assert.Equal(HttpStatusCode.OK, retireResponse.StatusCode);
        var retiredTruck = JsonNode.Parse(await retireResponse.Content.ReadAsStringAsync())!.AsObject();
        Assert.False(retiredTruck["isActive"]!.GetValue<bool>());
        Assert.Null(retiredTruck["assignedDriverId"]);

        var driverActiveTrucksResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/trucks",
            driver.Token);
        Assert.Equal(HttpStatusCode.OK, driverActiveTrucksResponse.StatusCode);
        var activeTrucks = JsonNode.Parse(await driverActiveTrucksResponse.Content.ReadAsStringAsync())!.AsArray();
        Assert.DoesNotContain(activeTrucks, item => Guid.Parse(item!["id"]!.GetValue<string>()) == truckId);

        var adminAllTrucksResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/trucks?includeRetired=true",
            admin.Token);
        Assert.Equal(HttpStatusCode.OK, adminAllTrucksResponse.StatusCode);
        var allTrucks = JsonNode.Parse(await adminAllTrucksResponse.Content.ReadAsStringAsync())!.AsArray();
        Assert.Contains(allTrucks, item => Guid.Parse(item!["id"]!.GetValue<string>()) == truckId);

        var driverRetiredListResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/trucks?includeRetired=true",
            driver.Token);
        Assert.Equal(HttpStatusCode.Forbidden, driverRetiredListResponse.StatusCode);

        var driverRetiredGetResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/trucks/{truckId}",
            driver.Token);
        Assert.Equal(HttpStatusCode.NotFound, driverRetiredGetResponse.StatusCode);

        var driverRetireResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            $"/api/trucks/{truckId}/retire",
            driver.Token);
        Assert.Equal(HttpStatusCode.Forbidden, driverRetireResponse.StatusCode);

        var currentAfterRetirementResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/trucks/me",
            driver.Token);
        Assert.Equal(HttpStatusCode.NotFound, currentAfterRetirementResponse.StatusCode);

        var retiredAssignmentResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            $"/api/trucks/assignments/{driver.UserId}",
            admin.Token,
            new { truckId });
        Assert.Equal(HttpStatusCode.Conflict, retiredAssignmentResponse.StatusCode);

        var retiredTripCreateResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/api/trips",
            driver.Token,
            new
            {
                date = "2026-09-27",
                truckId,
                startKm = 125m,
                endKm = 150m,
                pickupLocation = "B",
                dropoffLocation = "C"
            });
        Assert.Equal(HttpStatusCode.Conflict, retiredTripCreateResponse.StatusCode);

        var unknownTripCreateResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/api/trips",
            driver.Token,
            new
            {
                date = "2026-09-27",
                truckId = Guid.NewGuid(),
                startKm = 125m,
                endKm = 150m,
                pickupLocation = "B",
                dropoffLocation = "C"
            });
        Assert.Equal(HttpStatusCode.BadRequest, unknownTripCreateResponse.StatusCode);

        var retiredTripResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/trips/{tripId}",
            driver.Token);
        Assert.Equal(HttpStatusCode.OK, retiredTripResponse.StatusCode);
        var retainedTrip = JsonNode.Parse(await retiredTripResponse.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(truckId, Guid.Parse(retainedTrip["truckId"]!.GetValue<string>()));
    }

    [Fact]
    public async Task AssigningAnotherTruckReplacesCurrentAssignment_AndRetiredTruckCannotBeAssigned()
    {
        var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var admin = await RegisterAndLoginAsync(
            client,
            $"assignment-admin-{suffix}@example.com",
            "Passw0rd1",
            "Assignment Admin");
        await _factory.PromoteToAdminAsync(admin.UserId);
        admin = await LoginAsync(client, $"assignment-admin-{suffix}@example.com", "Passw0rd1");
        var driverOne = await RegisterAndLoginAsync(
            client,
            $"assignment-driver-one-{suffix}@example.com",
            "Passw0rd1",
            "Assignment Driver One");
        var driverTwo = await RegisterAndLoginAsync(
            client,
            $"assignment-driver-two-{suffix}@example.com",
            "Passw0rd1",
            "Assignment Driver Two");

        var firstTruck = await CreateTruckAsync(client, admin.Token, $"ASSIGN-A-{suffix}");
        var secondTruck = await CreateTruckAsync(client, admin.Token, $"ASSIGN-B-{suffix}");

        var firstAssignment = await SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            $"/api/trucks/{firstTruck}/assignment",
            admin.Token,
            new { driverId = driverOne.UserId });
        Assert.Equal(HttpStatusCode.OK, firstAssignment.StatusCode);

        var replacementAssignment = await AssignTruckAsync(client, admin.Token, driverOne.UserId, secondTruck);
        Assert.Equal(HttpStatusCode.OK, replacementAssignment.StatusCode);

        var driverOneCurrent = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/trucks/me", driverOne.Token);
        Assert.Equal(HttpStatusCode.OK, driverOneCurrent.StatusCode);
        Assert.Equal(secondTruck, Guid.Parse(JsonNode.Parse(await driverOneCurrent.Content.ReadAsStringAsync())!["id"]!.GetValue<string>()));

        var firstTruckDetails = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/trucks/{firstTruck}", admin.Token);
        Assert.Null(JsonNode.Parse(await firstTruckDetails.Content.ReadAsStringAsync())!["assignedDriverId"]);

        var secondDriverAssignment = await AssignTruckAsync(client, admin.Token, driverTwo.UserId, secondTruck);
        Assert.Equal(HttpStatusCode.OK, secondDriverAssignment.StatusCode);

        var driverOneAfterDisplacement = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/trucks/me", driverOne.Token);
        Assert.Equal(HttpStatusCode.NotFound, driverOneAfterDisplacement.StatusCode);

        var retireResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            $"/api/trucks/{secondTruck}/retire",
            admin.Token);
        Assert.Equal(HttpStatusCode.OK, retireResponse.StatusCode);

        var retiredAssignment = await AssignTruckAsync(client, admin.Token, driverOne.UserId, secondTruck);
        Assert.Equal(HttpStatusCode.Conflict, retiredAssignment.StatusCode);
    }

    private async Task<Guid> CreateTruckAsync(HttpClient client, string token, string registrationNumber)
    {
        var response = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/api/trucks",
            token,
            new { registrationNumber });
        response.EnsureSuccessStatusCode();
        return Guid.Parse(JsonNode.Parse(await response.Content.ReadAsStringAsync())!["id"]!.GetValue<string>());
    }

    private static Task<HttpResponseMessage> AssignTruckAsync(
        HttpClient client,
        string token,
        string driverId,
        Guid truckId) =>
        SendAuthorizedAsync(
            client,
            HttpMethod.Put,
            $"/api/trucks/assignments/{driverId}",
            token,
            new { truckId });

    private static async Task<(string Token, string UserId)> RegisterAndLoginAsync(
        HttpClient client,
        string email,
        string password,
        string name)
    {
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, password, name });
        registerResponse.EnsureSuccessStatusCode();
        return await LoginAsync(client, email, password);
    }

    private static async Task<(string Token, string UserId)> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password });
        response.EnsureSuccessStatusCode();
        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        return (
            payload["token"]!.GetValue<string>(),
            payload["user"]!["id"]!.GetValue<string>());
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
