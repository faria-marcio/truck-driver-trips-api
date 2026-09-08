using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDriverTrips.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTripContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "Trips");

            migrationBuilder.AddColumn<string>(
                name: "BolNumber",
                table: "Trips",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionAmount",
                table: "Trips",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EndKm",
                table: "Trips",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FuelCostAmount",
                table: "Trips",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Trips",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StartKm",
                table: "Trips",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TruckId",
                table: "Trips",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Trips",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "WaitTimeMinutes",
                table: "Trips",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Preserve the old manually-entered distance for rows created before the
            // odometer-based contract. New rows always derive DistanceKm in the API.
            migrationBuilder.Sql(
                "UPDATE \"Trips\" SET \"EndKm\" = \"DistanceKm\" WHERE \"EndKm\" = 0 AND \"DistanceKm\" > 0;");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DriverId_TruckId_Date",
                table: "Trips",
                columns: new[] { "DriverId", "TruckId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TruckId_Date",
                table: "Trips",
                columns: new[] { "TruckId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_DriverId_TruckId_Date",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_TruckId_Date",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "BolNumber",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "CommissionAmount",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "EndKm",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "FuelCostAmount",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "StartKm",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "TruckId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "WaitTimeMinutes",
                table: "Trips");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "EndTime",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));
        }
    }
}
