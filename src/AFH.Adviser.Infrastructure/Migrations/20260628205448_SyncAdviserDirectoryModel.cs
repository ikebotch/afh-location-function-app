using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Adviser.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncAdviserDirectoryModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoverageRegions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    LeadAdviserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LeadAdviserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Postcodes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Skills = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CoverageRadiusMiles = table.Column<double>(type: "float", nullable: false),
                    MaxTravelTimeMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageRegions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdviserRegionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdviserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AdviserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsLead = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdviserRegionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdviserRegionAssignments_CoverageRegions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "CoverageRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserRegionAssignments_AdviserId_IsActive",
                table: "AdviserRegionAssignments",
                columns: new[] { "AdviserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserRegionAssignments_RegionId_AdviserId",
                table: "AdviserRegionAssignments",
                columns: new[] { "RegionId", "AdviserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRegions_Code",
                table: "CoverageRegions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRegions_IsActive_Name",
                table: "CoverageRegions",
                columns: new[] { "IsActive", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdviserRegionAssignments");

            migrationBuilder.DropTable(
                name: "CoverageRegions");
        }
    }
}
