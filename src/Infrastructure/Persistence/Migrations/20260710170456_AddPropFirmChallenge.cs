using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPropFirmChallenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropFirmChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TradingAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StartingBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    ProfitTargetPercent = table.Column<double>(type: "double precision", nullable: false),
                    MaxDailyLossPercent = table.Column<double>(type: "double precision", nullable: false),
                    MaxTotalDrawdownPercent = table.Column<double>(type: "double precision", nullable: false),
                    DrawdownMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MinTradingDays = table.Column<int>(type: "integer", nullable: false),
                    SingleStep = table.Column<bool>(type: "boolean", nullable: false),
                    Phase = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Breach = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CurrentEquity = table.Column<decimal>(type: "numeric", nullable: false),
                    PeakEquity = table.Column<decimal>(type: "numeric", nullable: false),
                    DailyStartEquity = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrentDay = table.Column<DateOnly>(type: "date", nullable: true),
                    TradingDaysCount = table.Column<int>(type: "integer", nullable: false),
                    LastEquityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropFirmChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropFirmChallenges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropFirmChallenges_UserId_CreatedAt",
                table: "PropFirmChallenges",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropFirmChallenges");
        }
    }
}
