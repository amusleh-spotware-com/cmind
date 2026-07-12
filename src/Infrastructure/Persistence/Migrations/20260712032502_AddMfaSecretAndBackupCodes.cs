using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaSecretAndBackupCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MfaSecret",
                table: "Users");

            migrationBuilder.AddColumn<byte[]>(
                name: "EncryptedMfaSecret",
                table: "Users",
                type: "bytea",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MfaBackupCode",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaBackupCode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MfaBackupCode_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MfaBackupCode_UserId_CodeHash",
                table: "MfaBackupCode",
                columns: new[] { "UserId", "CodeHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MfaBackupCode");

            migrationBuilder.DropColumn(
                name: "EncryptedMfaSecret",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "MfaSecret",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }
    }
}
