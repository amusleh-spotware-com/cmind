using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CopyLotSanityCeiling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LotSanityAbsoluteMaxLots",
                table: "CopyDestinations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "LotSanityMasterMultiple",
                table: "CopyDestinations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LotSanityAbsoluteMaxLots",
                table: "CopyDestinations");

            migrationBuilder.DropColumn(
                name: "LotSanityMasterMultiple",
                table: "CopyDestinations");
        }
    }
}
