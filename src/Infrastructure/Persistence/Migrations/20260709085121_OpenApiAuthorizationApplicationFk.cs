using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OpenApiAuthorizationApplicationFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OpenApiAuthorizations_ApplicationId",
                table: "OpenApiAuthorizations",
                column: "ApplicationId");

            migrationBuilder.AddForeignKey(
                name: "FK_OpenApiAuthorizations_OpenApiApplications_ApplicationId",
                table: "OpenApiAuthorizations",
                column: "ApplicationId",
                principalTable: "OpenApiApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpenApiAuthorizations_OpenApiApplications_ApplicationId",
                table: "OpenApiAuthorizations");

            migrationBuilder.DropIndex(
                name: "IX_OpenApiAuthorizations_ApplicationId",
                table: "OpenApiAuthorizations");
        }
    }
}
