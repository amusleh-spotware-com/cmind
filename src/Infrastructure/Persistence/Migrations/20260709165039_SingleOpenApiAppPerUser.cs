using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SingleOpenApiAppPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OpenApiApplications_UserId_Name",
                table: "OpenApiApplications");

            migrationBuilder.CreateIndex(
                name: "IX_OpenApiApplications_UserId",
                table: "OpenApiApplications",
                column: "UserId",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OpenApiApplications_UserId",
                table: "OpenApiApplications");

            migrationBuilder.CreateIndex(
                name: "IX_OpenApiApplications_UserId_Name",
                table: "OpenApiApplications",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }
    }
}
