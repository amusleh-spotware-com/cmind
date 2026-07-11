using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OpenApiTokenRefreshEscalation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveRefreshFailures",
                table: "OpenApiAuthorizations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RefreshCriticalAlerted",
                table: "OpenApiAuthorizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsecutiveRefreshFailures",
                table: "OpenApiAuthorizations");

            migrationBuilder.DropColumn(
                name: "RefreshCriticalAlerted",
                table: "OpenApiAuthorizations");
        }
    }
}
