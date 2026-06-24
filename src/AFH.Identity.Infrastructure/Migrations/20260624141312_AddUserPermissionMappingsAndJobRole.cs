using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPermissionMappingsAndJobRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JobRole",
                table: "DomainUserProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DomainUserPermissionMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalSubject = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    IsGranted = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainUserPermissionMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserPermissionMappings_Email",
                table: "DomainUserPermissionMappings",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserPermissionMappings_ExternalSubject",
                table: "DomainUserPermissionMappings",
                column: "ExternalSubject");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserPermissionMappings_PermissionId_IsEnabled",
                table: "DomainUserPermissionMappings",
                columns: new[] { "PermissionId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserPermissionMappings_UserProfileId",
                table: "DomainUserPermissionMappings",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserPermissionMappings_UserProfileId_PermissionId_ExternalSubject_Email",
                table: "DomainUserPermissionMappings",
                columns: new[] { "UserProfileId", "PermissionId", "ExternalSubject", "Email" },
                unique: true,
                filter: "[UserProfileId] IS NOT NULL AND [ExternalSubject] IS NOT NULL AND [Email] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainUserPermissionMappings");

            migrationBuilder.DropColumn(
                name: "JobRole",
                table: "DomainUserProfiles");
        }
    }
}
