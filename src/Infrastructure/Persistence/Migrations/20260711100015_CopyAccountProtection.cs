using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CopyAccountProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountProtectionMode",
                table: "CopyDestinations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "AccountProtectionStopEquity",
                table: "CopyDestinations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "AccountProtectionTakeEquity",
                table: "CopyDestinations",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountProtectionMode",
                table: "CopyDestinations");

            migrationBuilder.DropColumn(
                name: "AccountProtectionStopEquity",
                table: "CopyDestinations");

            migrationBuilder.DropColumn(
                name: "AccountProtectionTakeEquity",
                table: "CopyDestinations");
        }
    }
}
