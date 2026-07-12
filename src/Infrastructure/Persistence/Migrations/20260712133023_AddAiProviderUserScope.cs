using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiProviderUserScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiProviderCredentials_IsActive",
                table: "AiProviderCredentials");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "AiProviderCredentials",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderCredentials_IsActive",
                table: "AiProviderCredentials",
                column: "IsActive",
                unique: true,
                filter: "\"OwnerUserId\" IS NULL AND \"IsActive\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderCredentials_OwnerUserId_IsActive",
                table: "AiProviderCredentials",
                columns: new[] { "OwnerUserId", "IsActive" },
                unique: true,
                filter: "\"OwnerUserId\" IS NOT NULL AND \"IsActive\" = true AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiProviderCredentials_IsActive",
                table: "AiProviderCredentials");

            migrationBuilder.DropIndex(
                name: "IX_AiProviderCredentials_OwnerUserId_IsActive",
                table: "AiProviderCredentials");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "AiProviderCredentials");

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderCredentials_IsActive",
                table: "AiProviderCredentials",
                column: "IsActive",
                unique: true,
                filter: "\"IsActive\" = true AND \"IsDeleted\" = false");
        }
    }
}
