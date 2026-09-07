using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Location.Infrastructure.Persistence.PolicyStore.Migrations;

[DbContext(typeof(LocationPolicyDbContext))]
[Migration("20260907121000_AddXPlanAdviserIdToReferenceCache")]
public partial class AddXPlanAdviserIdToReferenceCache : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "XPlanAdviserId",
            table: "AdviserReferenceCache",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "XPlanAdviserId",
            table: "AdviserReferenceCache");
    }
}
