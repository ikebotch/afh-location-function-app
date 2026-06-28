using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainAccessScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DomainAccessScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Area = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ScopeValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainAccessScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DomainUserAccessScopeMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccessScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalSubject = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainUserAccessScopeMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DomainAccessScopes_Area_IsEnabled",
                table: "DomainAccessScopes",
                columns: new[] { "Area", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_DomainAccessScopes_Area_ScopeType_ScopeValue",
                table: "DomainAccessScopes",
                columns: new[] { "Area", "ScopeType", "ScopeValue" },
                unique: true,
                filter: "[ScopeValue] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserAccessScopeMappings_AccessScopeId_IsEnabled",
                table: "DomainUserAccessScopeMappings",
                columns: new[] { "AccessScopeId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserAccessScopeMappings_Email",
                table: "DomainUserAccessScopeMappings",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserAccessScopeMappings_ExternalSubject",
                table: "DomainUserAccessScopeMappings",
                column: "ExternalSubject");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserAccessScopeMappings_UserProfileId",
                table: "DomainUserAccessScopeMappings",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserAccessScopeMappings_UserProfileId_ExternalSubject_Email_AccessScopeId",
                table: "DomainUserAccessScopeMappings",
                columns: new[] { "UserProfileId", "ExternalSubject", "Email", "AccessScopeId" },
                unique: true,
                filter: "[UserProfileId] IS NOT NULL AND [ExternalSubject] IS NOT NULL AND [Email] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainAccessScopes");

            migrationBuilder.DropTable(
                name: "DomainUserAccessScopeMappings");
        }
    }
}
