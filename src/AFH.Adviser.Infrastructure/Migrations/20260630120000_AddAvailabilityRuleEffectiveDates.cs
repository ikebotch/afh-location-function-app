using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Adviser.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Migration("20260630120000_AddAvailabilityRuleEffectiveDates")]
    public partial class AddAvailabilityRuleEffectiveDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EffectiveFrom",
                table: "AdviserWorkingPatternRules",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EffectiveTo",
                table: "AdviserWorkingPatternRules",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "AdviserWorkingPatternRules");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                table: "AdviserWorkingPatternRules");
        }
    }
}
