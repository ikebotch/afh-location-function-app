using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Location.Infrastructure.Persistence.PolicyStore.Migrations
{
    /// <inheritdoc />
    public partial class LocationErrorManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdviserReferenceCache",
                columns: table => new
                {
                    AdviserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MailboxUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HomePostcode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BaseOfficeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TeamName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ManagerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SkillsCsv = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Rating = table.Column<double>(type: "float", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsBookable = table.Column<bool>(type: "bit", nullable: false),
                    CoverageRadiusMiles = table.Column<double>(type: "float", nullable: true),
                    MaxTravelTimeMinutes = table.Column<int>(type: "int", nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdviserReferenceCache", x => x.AdviserId);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ContextId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExceptionMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GeoCacheEntries",
                columns: table => new
                {
                    CacheKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeoCacheEntries", x => x.CacheKey);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationOperationAudit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FunctionName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    QueryString = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OperationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ErrorType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationOperationAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RouteCacheEntries",
                columns: table => new
                {
                    CacheKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    EtaMinutes = table.Column<int>(type: "int", nullable: false),
                    DistanceMiles = table.Column<double>(type: "float", nullable: false),
                    Confidence = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteCacheEntries", x => x.CacheKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserReferenceCache_LastSyncedUtc",
                table: "AdviserReferenceCache",
                column: "LastSyncedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLogs_Category_OccurredUtc",
                table: "ApplicationLogs",
                columns: new[] { "Category", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLogs_CorrelationId",
                table: "ApplicationLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLogs_OccurredUtc",
                table: "ApplicationLogs",
                column: "OccurredUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLogs_Operation_OccurredUtc",
                table: "ApplicationLogs",
                columns: new[] { "Operation", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GeoCacheEntries_ExpiresUtc",
                table: "GeoCacheEntries",
                column: "ExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOperationAudit_CorrelationId",
                table: "IntegrationOperationAudit",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOperationAudit_CreatedUtc",
                table: "IntegrationOperationAudit",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOperationAudit_FunctionName_CreatedUtc",
                table: "IntegrationOperationAudit",
                columns: new[] { "FunctionName", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RouteCacheEntries_ExpiresUtc",
                table: "RouteCacheEntries",
                column: "ExpiresUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdviserReferenceCache");

            migrationBuilder.DropTable(
                name: "ApplicationLogs");

            migrationBuilder.DropTable(
                name: "GeoCacheEntries");

            migrationBuilder.DropTable(
                name: "IntegrationOperationAudit");

            migrationBuilder.DropTable(
                name: "RouteCacheEntries");
        }
    }
}
