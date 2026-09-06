# truck-driver-trips-api

ASP.NET Core 10 REST API backend for truck-driver trip logging.

## Features

- JWT authentication with ASP.NET Core Identity password hashing
- Role-based authorization (`Driver`, `Admin`)
- Trip CRUD with strict ownership rules in backend
- Consistent error responses using `ProblemDetails`
- EF Core + SQLite standalone, with Aspire-managed PostgreSQL for development
- Swagger/OpenAPI in development

## Prerequisites

- .NET SDK 10+
- Aspire CLI 13.5.x (for the distributed AppHost)

## Project structure

- `/src/TruckDriverTrips.Api` - API project
- `/src/TruckDriverTrips.AppHost` - Aspire orchestration project
- `/src/TruckDriverTrips.ServiceDefaults` - shared health checks and OpenTelemetry defaults
- `/tests/TruckDriverTrips.Api.Tests` - focused integration tests

## Configuration

Settings are read from `appsettings*.json` and environment variables.

### Required JWT configuration

Use environment variables in non-local environments:

- `Jwt__Key` (minimum 32 chars; never commit real secrets)
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__AccessTokenMinutes`

For local Aspire runs, set the secret AppHost parameters `jwt-key` and `nextauth-secret` through
Aspire/AppHost user secrets. They are intentionally not committed to `appsettings.json`.
For example:

```bash
dotnet user-secrets --project src/TruckDriverTrips.AppHost set Parameters:jwt-key "$(openssl rand -base64 48)"
dotnet user-secrets --project src/TruckDriverTrips.AppHost set Parameters:nextauth-secret "$(openssl rand -base64 48)"
```

### CORS configuration

Set allowed frontend origins with:

- `Cors__AllowedOrigins__0=http://localhost:3000`
- `Cors__AllowedOrigins__1=https://your-frontend.example`

## Local setup

```bash
dotnet restore
dotnet build TruckDriverTrips.slnx
dotnet run --project src/TruckDriverTrips.Api
```

The app uses SQLite with `Data Source=truck-driver-trips.db` by default.

## Aspire development

The C# AppHost at `/src/TruckDriverTrips.AppHost` orchestrates the API, an Aspire-managed
PostgreSQL server/database, and the sibling Next.js frontend. It searches parent directories
for a `truck-driver-trips-web` checkout, which supports both a normal repository layout
(`..\truck-driver-trips-web`) and this repository's nested worktree layout
(`..\..\..\truck-driver-trips-web` from the API repository root). It does not modify that
repository and fails clearly if no checkout is found.

Start the distributed application with:

```bash
aspire start --non-interactive --apphost src/TruckDriverTrips.AppHost
```

Aspire supplies the `tripsdb` connection string to the API and waits for PostgreSQL before
starting the API. It also injects the API endpoint as both `NEXT_PUBLIC_API_URL` and `API_URL`
for the frontend, along with `NEXTAUTH_SECRET` and the frontend endpoint as `NEXTAUTH_URL`, and
configures the API CORS origin from the Aspire frontend endpoint. The API
continues to use the SQLite `DefaultConnection` when run directly without an Aspire connection.

Aspire's PostgreSQL resource is persistent and is not deleted automatically by this project.
Docker Desktop is the recommended container runtime. Podman can work, but its networking and
persistent-volume behavior may require a clean, manually managed resource when recovering from
stale state; do not use destructive `aspire stop --force` cleanup unless data loss is intended.

## Database and migrations

The existing initial migration was generated for SQLite. Standalone SQLite runs apply it with
`Database.Migrate()`. Aspire PostgreSQL development databases use `EnsureCreated()` from the
provider model instead, avoiding SQLite-specific store types and annotations. Generate and test
a provider-specific migration before introducing PostgreSQL schema evolution beyond development.

If you want migration-based flow:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/TruckDriverTrips.Api
dotnet ef database update --project src/TruckDriverTrips.Api
```

## PostgreSQL standalone configuration

To run the API directly against PostgreSQL, provide a `tripsdb` connection string
(`ConnectionStrings__tripsdb`) and install/configure PostgreSQL separately. When `tripsdb` is
absent, the API uses the SQLite `DefaultConnection` fallback.

## API behavior

### Auth

- `POST /api/auth/register`
  - Creates driver account, assigns `Driver` role.
  - Returns `{ token, user: { id, email, name, role } }`.
  - `409` when email already exists.
- `POST /api/auth/login`
  - Returns `{ token, user: { id, email, name, role } }`.
  - `401` for invalid credentials.

### Trips (JWT required)

- `GET /api/trips`
  - Driver: only own trips.
  - Admin: all trips.
- `POST /api/trips`
  - Creates trip owned by authenticated driver.
- `GET /api/trips/{id}` / `PUT /api/trips/{id}` / `DELETE /api/trips/{id}`
  - Allowed for owner or admin only.
  - Returns `404` when trip is not accessible.

Trip fields:

- `date` (`DateOnly`)
- `startTime` (`TimeOnly`)
- `endTime` (`TimeOnly`, must be greater than `startTime`)
- `distanceKm` (> 0)
- `pickupLocation`
- `dropoffLocation`
- server-managed `driverId`, `createdAtUtc`, `updatedAtUtc`

## Frontend integration

The frontend should send an `Authorization` header with the JWT bearer token after login and consume this login shape:

```json
{
  "token": "...",
  "user": {
    "id": "...",
    "email": "driver@example.com",
    "name": "Driver Name",
    "role": "Driver"
  }
}
```

## Test

```bash
dotnet test TruckDriverTrips.slnx
```
