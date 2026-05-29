using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueIndexFilterSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CBots_UserId_Name",
                table: "CBots");

            migrationBuilder.DropIndex(
                name: "IX_CBotSourceProjects_UserId_Name",
                table: "CBotSourceProjects");

            migrationBuilder.CreateIndex(
                name: "IX_CBots_UserId_Name",
                table: "CBots",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CBotSourceProjects_UserId_Name",
                table: "CBotSourceProjects",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CBots_UserId_Name",
                table: "CBots");

            migrationBuilder.DropIndex(
                name: "IX_CBotSourceProjects_UserId_Name",
                table: "CBotSourceProjects");

            migrationBuilder.CreateIndex(
                name: "IX_CBots_UserId_Name",
                table: "CBots",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CBotSourceProjects_UserId_Name",
                table: "CBotSourceProjects",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }
    }
}
