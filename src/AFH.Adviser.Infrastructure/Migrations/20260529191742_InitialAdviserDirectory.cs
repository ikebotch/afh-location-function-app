using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Adviser.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAdviserDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DomainRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
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
                name: "DomainUserRoleMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "OrganisationAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Context = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AssignmentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OrganisationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Region = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AdviserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Channels = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DomainRolePermissions_RoleId_Permission",
                table: "DomainRolePermissions",
                columns: new[] { "RoleId", "Permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DomainRoles_Role",
                table: "DomainRoles",
                column: "Role",
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
                name: "IX_OrganisationAssignments_AdviserId",
                table: "OrganisationAssignments",
                column: "AdviserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationAssignments_ClientId",
                table: "OrganisationAssignments",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationAssignments_Context_AssignmentType_IsEnabled_Priority",
                table: "OrganisationAssignments",
                columns: new[] { "Context", "AssignmentType", "IsEnabled", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationAssignments_OrganisationId",
                table: "OrganisationAssignments",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationAssignments_Region",
                table: "OrganisationAssignments",
                column: "Region");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainRolePermissions");

            migrationBuilder.DropTable(
                name: "DomainRoles");

            migrationBuilder.DropTable(
                name: "DomainUserRoleMappings");

            migrationBuilder.DropTable(
                name: "OrganisationAssignments");
        }
    }
}
