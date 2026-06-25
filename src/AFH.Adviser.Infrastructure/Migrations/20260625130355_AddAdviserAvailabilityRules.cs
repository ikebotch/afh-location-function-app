using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Adviser.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdviserAvailabilityRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdviserAvailabilityRuleSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectContext = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MinimumAppointmentMinutes = table.Column<int>(type: "int", nullable: false),
                    DefaultWorkingDayStart = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DefaultWorkingDayEnd = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CapacityWindowDays = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdviserAvailabilityRuleSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdviserCapacityLimitRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleSetId = table.Column<int>(type: "int", nullable: false),
                    AdviserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaxActiveBookings = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdviserCapacityLimitRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdviserCapacityLimitRules_AdviserAvailabilityRuleSets_RuleSetId",
                        column: x => x.RuleSetId,
                        principalTable: "AdviserAvailabilityRuleSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdviserWorkingPatternRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleSetId = table.Column<int>(type: "int", nullable: false),
                    AdviserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Start = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    End = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdviserWorkingPatternRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdviserWorkingPatternRules_AdviserAvailabilityRuleSets_RuleSetId",
                        column: x => x.RuleSetId,
                        principalTable: "AdviserAvailabilityRuleSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserAvailabilityRuleSets_ProjectContext_IsActive_UpdatedUtc",
                table: "AdviserAvailabilityRuleSets",
                columns: new[] { "ProjectContext", "IsActive", "UpdatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserCapacityLimitRules_RuleSetId_AdviserId_IsActive",
                table: "AdviserCapacityLimitRules",
                columns: new[] { "RuleSetId", "AdviserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserWorkingPatternRules_RuleSetId_AdviserId_IsActive",
                table: "AdviserWorkingPatternRules",
                columns: new[] { "RuleSetId", "AdviserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdviserCapacityLimitRules");

            migrationBuilder.DropTable(
                name: "AdviserWorkingPatternRules");

            migrationBuilder.DropTable(
                name: "AdviserAvailabilityRuleSets");
        }
    }
}
