using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Location.Infrastructure.Persistence.PolicyStore.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationAvailabilityCaching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErrorRecordEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Path = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Method = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorRecordEntity", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorRecordEntity_Code",
                table: "ErrorRecordEntity",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorRecordEntity_OccurredUtc",
                table: "ErrorRecordEntity",
                column: "OccurredUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErrorRecordEntity");
        }
    }
}
