using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyStrengthSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "currency_strength_snapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOf = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RankingJson = table.Column<string>(type: "jsonb", nullable: false),
                    HorizonsJson = table.Column<string>(type: "jsonb", nullable: false),
                    IndicatorsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Narrative = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CalendarKnownAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currency_strength_snapshot", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_currency_strength_snapshot_AsOf",
                table: "currency_strength_snapshot",
                column: "AsOf");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "currency_strength_snapshot");
        }
    }
}
