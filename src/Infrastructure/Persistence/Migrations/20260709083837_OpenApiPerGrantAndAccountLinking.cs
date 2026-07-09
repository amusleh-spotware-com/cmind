using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OpenApiPerGrantAndAccountLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CtidTraderAccountId",
                table: "OpenApiAuthorizations",
                newName: "CtidUserId");

            migrationBuilder.RenameIndex(
                name: "IX_OpenApiAuthorizations_UserId_CtidTraderAccountId",
                table: "OpenApiAuthorizations",
                newName: "IX_OpenApiAuthorizations_UserId_CtidUserId");

            migrationBuilder.AddColumn<long>(
                name: "CtidTraderAccountId",
                table: "TradingAccounts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkMethod",
                table: "TradingAccounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "OpenApiAuthorizationId",
                table: "TradingAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CtidUserId",
                table: "CTids",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CtidTraderAccountId",
                table: "TradingAccounts");

            migrationBuilder.DropColumn(
                name: "LinkMethod",
                table: "TradingAccounts");

            migrationBuilder.DropColumn(
                name: "OpenApiAuthorizationId",
                table: "TradingAccounts");

            migrationBuilder.DropColumn(
                name: "CtidUserId",
                table: "CTids");

            migrationBuilder.RenameColumn(
                name: "CtidUserId",
                table: "OpenApiAuthorizations",
                newName: "CtidTraderAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_OpenApiAuthorizations_UserId_CtidUserId",
                table: "OpenApiAuthorizations",
                newName: "IX_OpenApiAuthorizations_UserId_CtidTraderAccountId");
        }
    }
}
