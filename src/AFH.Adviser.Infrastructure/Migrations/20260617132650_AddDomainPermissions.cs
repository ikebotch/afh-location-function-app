using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Adviser.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainPermissions : Migration
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

            migrationBuilder.Sql("""
                INSERT INTO [dbo].[DomainPermissions]
                    ([Id], [Permission], [DisplayName], [Description], [Category], [IsEnabled], [CreatedUtc], [UpdatedUtc])
                SELECT NEWID(),
                       rp.[Permission],
                       REPLACE(rp.[Permission], '.', ' '),
                       NULL,
                       CASE
                           WHEN CHARINDEX('.', rp.[Permission]) > 1 THEN LEFT(rp.[Permission], CHARINDEX('.', rp.[Permission]) - 1)
                           ELSE 'General'
                       END,
                       1,
                       SYSUTCDATETIME(),
                       NULL
                FROM (
                    SELECT DISTINCT [Permission]
                    FROM [dbo].[DomainRolePermissions]
                    WHERE [Permission] IS NOT NULL AND LTRIM(RTRIM([Permission])) <> ''
                ) rp
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[DomainPermissions] existing
                    WHERE existing.[Permission] = rp.[Permission]
                );
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "PermissionId",
                table: "DomainRolePermissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
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

            migrationBuilder.CreateTable(
                name: "DomainUserPermissionMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ExternalRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExternalGroupId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainUserPermissionMappings", x => x.Id);
                });

            migrationBuilder.DropIndex(
                name: "IX_DomainRolePermissions_RoleId_Permission",
                table: "DomainRolePermissions");

            migrationBuilder.DropColumn(
                name: "Permission",
                table: "DomainRolePermissions");

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
                name: "IX_DomainUserPermissionMappings_Email",
                table: "DomainUserPermissionMappings",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserPermissionMappings_ExternalGroupId",
                table: "DomainUserPermissionMappings",
                column: "ExternalGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserPermissionMappings_ExternalRole",
                table: "DomainUserPermissionMappings",
                column: "ExternalRole");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUserPermissionMappings_PermissionId_IsEnabled",
                table: "DomainUserPermissionMappings",
                columns: new[] { "PermissionId", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainUserPermissionMappings");

            migrationBuilder.DropIndex(
                name: "IX_DomainRolePermissions_RoleId_PermissionId",
                table: "DomainRolePermissions");

            migrationBuilder.AddColumn<string>(
                name: "Permission",
                table: "DomainRolePermissions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE rp
                SET [Permission] = p.[Permission]
                FROM [dbo].[DomainRolePermissions] rp
                JOIN [dbo].[DomainPermissions] p ON p.[Id] = rp.[PermissionId];
                """);

            migrationBuilder.DropColumn(
                name: "PermissionId",
                table: "DomainRolePermissions");

            migrationBuilder.CreateIndex(
                name: "IX_DomainRolePermissions_RoleId_Permission",
                table: "DomainRolePermissions",
                columns: new[] { "RoleId", "Permission" },
                unique: true);

            migrationBuilder.DropTable(
                name: "DomainPermissions");
        }
    }
}
