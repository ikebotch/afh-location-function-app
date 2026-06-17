using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentityPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DomainPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DomainRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainRolePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DomainRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DomainUserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalSubject = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AdviserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainUserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DomainUserRoleMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ExternalRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExternalGroupId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainUserRoleMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DomainPermissions_Category_IsEnabled",
                table: "DomainPermissions",
                columns: new[] { "Category", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_DomainPermissions_Permission",
                table: "DomainPermissions",
                column: "Permission",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DomainRolePermissions_RoleId_PermissionId",
                table: "DomainRolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DomainRoles_Role",
                table: "DomainRoles",
                column: "Role",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserProfiles_AdviserId",
                table: "DomainUserProfiles",
                column: "AdviserId");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserProfiles_Email",
                table: "DomainUserProfiles",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserProfiles_ExternalSubject",
                table: "DomainUserProfiles",
                column: "ExternalSubject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserRoleMappings_Email",
                table: "DomainUserRoleMappings",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserRoleMappings_ExternalGroupId",
                table: "DomainUserRoleMappings",
                column: "ExternalGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserRoleMappings_ExternalRole",
                table: "DomainUserRoleMappings",
                column: "ExternalRole");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserRoleMappings_RoleId_IsEnabled",
                table: "DomainUserRoleMappings",
                columns: new[] { "RoleId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserRoleMappings_UserProfileId",
                table: "DomainUserRoleMappings",
                column: "UserProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainPermissions");

            migrationBuilder.DropTable(
                name: "DomainRolePermissions");

            migrationBuilder.DropTable(
                name: "DomainRoles");

            migrationBuilder.DropTable(
                name: "DomainUserProfiles");

            migrationBuilder.DropTable(
                name: "DomainUserRoleMappings");
        }
    }
}
