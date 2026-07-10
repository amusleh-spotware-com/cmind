using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CopyAdvancedMirroringAndNodeAffinity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedNode",
                table: "CopyProfiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CopyPendingOrders",
                table: "CopyDestinations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CopyTrailingStop",
                table: "CopyDestinations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MirrorPartialClose",
                table: "CopyDestinations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MirrorScaleIn",
                table: "CopyDestinations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedNode",
                table: "CopyProfiles");

            migrationBuilder.DropColumn(
                name: "CopyPendingOrders",
                table: "CopyDestinations");

            migrationBuilder.DropColumn(
                name: "CopyTrailingStop",
                table: "CopyDestinations");

            migrationBuilder.DropColumn(
                name: "MirrorPartialClose",
                table: "CopyDestinations");

            migrationBuilder.DropColumn(
                name: "MirrorScaleIn",
                table: "CopyDestinations");
        }
    }
}
