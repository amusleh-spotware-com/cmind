using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedOpenApiApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsShared",
                table: "OpenApiApplications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_OpenApiApplications_IsShared",
                table: "OpenApiApplications",
                column: "IsShared",
                unique: true,
                filter: "\"IsShared\" = true AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OpenApiApplications_IsShared",
                table: "OpenApiApplications");

            migrationBuilder.DropColumn(
                name: "IsShared",
                table: "OpenApiApplications");
        }
    }
}
