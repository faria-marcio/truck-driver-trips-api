using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDriverTrips.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTruckManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The project is a POC and the database can be recreated when the
            // trip contract changes. Recreate Trips so the truck relationship is
            // created with a Guid foreign key instead of the legacy text column.
            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.CreateTable(
                name: "Trucks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Make = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    RetiredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AssignedDriverId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trucks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trucks_AspNetUsers_AssignedDriverId",
                        column: x => x.AssignedDriverId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TruckId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartKm = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    EndKm = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    DistanceKm = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    PickupLocation = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    DropoffLocation = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    BolNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FuelCostAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    WaitTimeMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DriverId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trips_AspNetUsers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Trips_Trucks_TruckId",
                        column: x => x.TruckId,
                        principalTable: "Trucks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trucks_AssignedDriverId",
                table: "Trucks",
                column: "AssignedDriverId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trucks_RegistrationNumber",
                table: "Trucks",
                column: "RegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DriverId_Date",
                table: "Trips",
                columns: new[] { "DriverId", "Date" });

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
            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropTable(
                name: "Trucks");

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DistanceKm = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    PickupLocation = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    DropoffLocation = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    DriverId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BolNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CommissionAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    EndKm = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    FuelCostAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    StartKm = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    TruckId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: "unknown"),
                    Version = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    WaitTimeMinutes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trips_AspNetUsers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DriverId_Date",
                table: "Trips",
                columns: new[] { "DriverId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DriverId_TruckId_Date",
                table: "Trips",
                columns: new[] { "DriverId", "TruckId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TruckId_Date",
                table: "Trips",
                columns: new[] { "TruckId", "Date" });
        }
    }
}
