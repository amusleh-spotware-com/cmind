using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEconomicEventAlert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currencies",
                table: "AlertRules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastTriggeredEventKey",
                table: "AlertRules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinImpactLevel",
                table: "AlertRules",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinutesBefore",
                table: "AlertRules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Trigger",
                table: "AlertRules",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "MarketWatch");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currencies",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "LastTriggeredEventKey",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "MinImpactLevel",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "MinutesBefore",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "Trigger",
                table: "AlertRules");
        }
    }
}
