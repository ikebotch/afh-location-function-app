using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Adviser.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdviserCapacityPeriodLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyLimit",
                table: "AdviserCapacityLimitRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyLimit",
                table: "AdviserCapacityLimitRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyLimit",
                table: "AdviserCapacityLimitRules",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyLimit",
                table: "AdviserCapacityLimitRules");

            migrationBuilder.DropColumn(
                name: "MonthlyLimit",
                table: "AdviserCapacityLimitRules");

            migrationBuilder.DropColumn(
                name: "WeeklyLimit",
                table: "AdviserCapacityLimitRules");
        }
    }
}
