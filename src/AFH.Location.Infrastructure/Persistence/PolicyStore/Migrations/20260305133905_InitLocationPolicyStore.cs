using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Location.Infrastructure.Persistence.PolicyStore.Migrations
{
    /// <inheritdoc />
    public partial class InitLocationPolicyStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvailabilityDefaultPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DefaultTravelBufferMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxTravelBufferMinutes = table.Column<int>(type: "int", nullable: false),
                    DefaultCompanyBufferMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxCompanyBufferMinutes = table.Column<int>(type: "int", nullable: false),
                    PreviousClientProximityMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityDefaultPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoverageAdviserPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdviserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RadiusMiles = table.Column<double>(type: "float", nullable: true),
                    MaxTravelTimeMinutes = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageAdviserPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoverageDefaultPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DefaultRadiusMiles = table.Column<double>(type: "float", nullable: false),
                    DefaultMaxTravelTimeMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageDefaultPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoverageRegionPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Region = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RadiusMiles = table.Column<double>(type: "float", nullable: true),
                    MaxTravelTimeMinutes = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageRegionPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchAuditRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    SearchHorizonMinutes = table.Column<int>(type: "int", nullable: false),
                    DestinationPostcode = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    RegionsCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CandidatesReturned = table.Column<int>(type: "int", nullable: false),
                    SelectedAdviserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SelectedAdviserRating = table.Column<double>(type: "float", nullable: true),
                    SelectedAdviserGoldStar = table.Column<bool>(type: "bit", nullable: true),
                    SelectedTravelMinutes = table.Column<int>(type: "int", nullable: true),
                    SelectedMaxTravelTimeMinutes = table.Column<int>(type: "int", nullable: true),
                    SelectedCompanyBufferMinutes = table.Column<int>(type: "int", nullable: true),
                    SelectedTravelBufferMinutes = table.Column<int>(type: "int", nullable: true),
                    SelectedOriginSource = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageAdviserPolicies_AdviserId",
                table: "CoverageAdviserPolicies",
                column: "AdviserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRegionPolicies_Region",
                table: "CoverageRegionPolicies",
                column: "Region",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchAuditRecords_CreatedUtc",
                table: "SearchAuditRecords",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SearchAuditRecords_RequestId",
                table: "SearchAuditRecords",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvailabilityDefaultPolicies");

            migrationBuilder.DropTable(
                name: "CoverageAdviserPolicies");

            migrationBuilder.DropTable(
                name: "CoverageDefaultPolicies");

            migrationBuilder.DropTable(
                name: "CoverageRegionPolicies");

            migrationBuilder.DropTable(
                name: "SearchAuditRecords");
        }
    }
}
