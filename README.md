# truck-driver-trips-api

ASP.NET Core 8 REST API backend for truck-driver trip logging.

## Features

- JWT authentication with ASP.NET Core Identity password hashing
- Role-based authorization (`Driver`, `Admin`)
- Trip CRUD with strict ownership rules in backend
- Consistent error responses using `ProblemDetails`
- EF Core + SQLite by default (local-friendly)
- Swagger/OpenAPI in development

## Prerequisites

- .NET SDK 8+

## Project structure

- `/src/TruckDriverTrips.Api` - API project
- `/tests/TruckDriverTrips.Api.Tests` - focused integration tests

## Configuration

Settings are read from `appsettings*.json` and environment variables.

### Required JWT configuration

Use environment variables in non-local environments:

- `Jwt__Key` (minimum 32 chars; never commit real secrets)
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__AccessTokenMinutes`

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

## Database and migrations

This MVP includes an initial EF Core migration and applies pending migrations on startup (`Database.Migrate()`), so local setup stays simple while keeping a migration-based workflow.

If you want migration-based flow:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/TruckDriverTrips.Api
dotnet ef database update --project src/TruckDriverTrips.Api
```

## PostgreSQL switch (optional)

SQLite is default. To switch to PostgreSQL:

1. Add `Npgsql.EntityFrameworkCore.PostgreSQL` package.
2. Replace `UseSqlite(...)` with `UseNpgsql(...)` in `Program.cs`.
3. Set `ConnectionStrings__DefaultConnection` to your PostgreSQL connection string.

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
