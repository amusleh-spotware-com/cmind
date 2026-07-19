using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCotReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cot");

            migrationBuilder.CreateTable(
                name: "market",
                schema: "cot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractCodeValue = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Exchange = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    Group = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MappedSymbolValue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "report",
                schema: "cot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractCodeValue = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MarketName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Combined = table.Column<bool>(type: "boolean", nullable: false),
                    ReportDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    KnownAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OpenInterest = table.Column<long>(type: "bigint", nullable: false),
                    OpenInterestChange = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "category_position",
                schema: "cot",
                columns: table => new
                {
                    Category = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CotReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Long = table.Column<long>(type: "bigint", nullable: false),
                    Short = table.Column<long>(type: "bigint", nullable: false),
                    Spread = table.Column<long>(type: "bigint", nullable: false),
                    TradersLong = table.Column<int>(type: "integer", nullable: true),
                    TradersShort = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_position", x => new { x.CotReportId, x.Category });
                    table.ForeignKey(
                        name: "FK_category_position_report_CotReportId",
                        column: x => x.CotReportId,
                        principalSchema: "cot",
                        principalTable: "report",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_market_ContractCodeValue",
                schema: "cot",
                table: "market",
                column: "ContractCodeValue",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_market_Group",
                schema: "cot",
                table: "market",
                column: "Group");

            migrationBuilder.CreateIndex(
                name: "IX_report_ContractCodeValue_Kind_Combined_ReportDate",
                schema: "cot",
                table: "report",
                columns: new[] { "ContractCodeValue", "Kind", "Combined", "ReportDate" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_report_KnownAt",
                schema: "cot",
                table: "report",
                column: "KnownAt");

            migrationBuilder.CreateIndex(
                name: "IX_report_MarketId_Kind_Combined_ReportDate",
                schema: "cot",
                table: "report",
                columns: new[] { "MarketId", "Kind", "Combined", "ReportDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_position",
                schema: "cot");

            migrationBuilder.DropTable(
                name: "market",
                schema: "cot");

            migrationBuilder.DropTable(
                name: "report",
                schema: "cot");
        }
    }
}
