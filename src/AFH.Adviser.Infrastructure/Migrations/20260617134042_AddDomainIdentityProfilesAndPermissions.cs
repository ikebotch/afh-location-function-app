using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Adviser.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainIdentityProfilesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserProfileId",
                table: "DomainUserRoleMappings",
                type: "uniqueidentifier",
                nullable: true);

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

            migrationBuilder.AddColumn<Guid>(
                name: "PermissionId",
                table: "DomainRolePermissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO [dbo].[DomainPermissions]
                    ([Id], [Permission], [DisplayName], [Description], [Category], [IsEnabled], [CreatedUtc], [UpdatedUtc])
                SELECT NEWID(),
                       legacy.[Permission],
                       REPLACE(legacy.[Permission], '.', ' '),
                       NULL,
                       CASE
                           WHEN CHARINDEX('.', legacy.[Permission]) > 1 THEN LEFT(legacy.[Permission], CHARINDEX('.', legacy.[Permission]) - 1)
                           ELSE 'General'
                       END,
                       1,
                       SYSUTCDATETIME(),
                       NULL
                FROM (
                    SELECT DISTINCT [Permission]
                    FROM [dbo].[DomainRolePermissions]
                    WHERE [Permission] IS NOT NULL AND LTRIM(RTRIM([Permission])) <> ''
                ) legacy
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[DomainPermissions] existing
                    WHERE existing.[Permission] = legacy.[Permission]
                );

                UPDATE rp
                SET [PermissionId] = p.[Id]
                FROM [dbo].[DomainRolePermissions] rp
                JOIN [dbo].[DomainPermissions] p ON p.[Permission] = rp.[Permission]
                WHERE rp.[PermissionId] IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "PermissionId",
                table: "DomainRolePermissions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_DomainRolePermissions_RoleId_Permission",
                table: "DomainRolePermissions");

            migrationBuilder.DropColumn(
                name: "Permission",
                table: "DomainRolePermissions");

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

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserRoleMappings_UserProfileId",
                table: "DomainUserRoleMappings",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_DomainRolePermissions_RoleId_PermissionId",
                table: "DomainRolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainUserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_DomainUserRoleMappings_UserProfileId",
                table: "DomainUserRoleMappings");

            migrationBuilder.DropIndex(
                name: "IX_DomainRolePermissions_RoleId_PermissionId",
                table: "DomainRolePermissions");

            migrationBuilder.AddColumn<string>(
                name: "Permission",
                table: "DomainRolePermissions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE rp
                SET [Permission] = p.[Permission]
                FROM [dbo].[DomainRolePermissions] rp
                JOIN [dbo].[DomainPermissions] p ON p.[Id] = rp.[PermissionId];
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Permission",
                table: "DomainRolePermissions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "UserProfileId",
                table: "DomainUserRoleMappings");

            migrationBuilder.DropColumn(
                name: "PermissionId",
                table: "DomainRolePermissions");

            migrationBuilder.DropTable(
                name: "DomainPermissions");

            migrationBuilder.CreateIndex(
                name: "IX_DomainRolePermissions_RoleId_Permission",
                table: "DomainRolePermissions",
                columns: new[] { "RoleId", "Permission" },
                unique: true);
        }
    }
}
