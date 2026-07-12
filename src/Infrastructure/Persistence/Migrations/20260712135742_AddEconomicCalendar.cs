using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEconomicCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "calendar");

            migrationBuilder.CreateTable(
                name: "economic_event",
                schema: "calendar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesCodeValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CountryValue = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Precision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceTimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Released = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_economic_event", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "series",
                schema: "calendar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesCodeValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CountryValue = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Cadence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DefaultImpact = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ImpactPrior = table.Column<double>(type: "double precision", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceSeriesId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "event_revision",
                schema: "calendar",
                columns: table => new
                {
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CalendarEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnownAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Actual = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Forecast = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Previous = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ImpactScore = table.Column<double>(type: "double precision", nullable: false),
                    ImpactLevel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ImpactModelVersion = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SourceRef = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RescheduledInstant = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_revision", x => new { x.CalendarEventId, x.Sequence });
                    table.ForeignKey(
                        name: "FK_event_revision_economic_event_CalendarEventId",
                        column: x => x.CalendarEventId,
                        principalSchema: "calendar",
                        principalTable: "economic_event",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_economic_event_EffectiveAt",
                schema: "calendar",
                table: "economic_event",
                column: "EffectiveAt");

            migrationBuilder.CreateIndex(
                name: "IX_economic_event_SeriesId_EffectiveAt",
                schema: "calendar",
                table: "economic_event",
                columns: new[] { "SeriesId", "EffectiveAt" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_event_revision_CalendarEventId_KnownAt",
                schema: "calendar",
                table: "event_revision",
                columns: new[] { "CalendarEventId", "KnownAt" });

            migrationBuilder.CreateIndex(
                name: "IX_series_SeriesCodeValue",
                schema: "calendar",
                table: "series",
                column: "SeriesCodeValue",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_revision",
                schema: "calendar");

            migrationBuilder.DropTable(
                name: "series",
                schema: "calendar");

            migrationBuilder.DropTable(
                name: "economic_event",
                schema: "calendar");
        }
    }
}
