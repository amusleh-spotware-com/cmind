using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CopyOrderTypeExpirySlippage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CopyMasterSlippage",
                table: "CopyDestinations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CopyOrderTypes",
                table: "CopyDestinations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "CopyPendingExpiry",
                table: "CopyDestinations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CopyMasterSlippage",
                table: "CopyDestinations");

            migrationBuilder.DropColumn(
                name: "CopyOrderTypes",
                table: "CopyDestinations");

            migrationBuilder.DropColumn(
                name: "CopyPendingExpiry",
                table: "CopyDestinations");
        }
    }
}
