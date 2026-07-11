using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CopyPropRuleGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PropRuleDailyLossCap",
                table: "CopyDestinations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PropRuleTrailingDrawdown",
                table: "CopyDestinations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PropRuleDailyLossCap",
                table: "CopyDestinations");

            migrationBuilder.DropColumn(
                name: "PropRuleTrailingDrawdown",
                table: "CopyDestinations");
        }
    }
}
