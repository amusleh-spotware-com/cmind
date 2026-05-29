using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EncryptProjectFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectFilesJson",
                table: "CBotSourceProjects");

            migrationBuilder.AddColumn<byte[]>(
                name: "EncryptedProjectFiles",
                table: "CBotSourceProjects",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptedProjectFiles",
                table: "CBotSourceProjects");

            migrationBuilder.AddColumn<string>(
                name: "ProjectFilesJson",
                table: "CBotSourceProjects",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
