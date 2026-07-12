using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTradingAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradingAgents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Archetype = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Autonomy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Aggressiveness = table.Column<double>(type: "double precision", nullable: false),
                    Patience = table.Column<double>(type: "double precision", nullable: false),
                    TrendBias = table.Column<double>(type: "double precision", nullable: false),
                    ObjectiveDrawdownWeight = table.Column<double>(type: "double precision", nullable: false),
                    EnvMaxDailyLossPercent = table.Column<double>(type: "double precision", nullable: true),
                    EnvMaxOpenExposureLots = table.Column<double>(type: "double precision", nullable: true),
                    EnvMaxPositionSizeLots = table.Column<double>(type: "double precision", nullable: true),
                    EnvMaxLeverage = table.Column<double>(type: "double precision", nullable: true),
                    EnvMaxConsecutiveLosses = table.Column<int>(type: "integer", nullable: true),
                    EnvMaxOrdersPerHour = table.Column<int>(type: "integer", nullable: true),
                    EnvAllowedSymbolsCsv = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    GoalsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ManagedAccountsCsv = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ConsentVersion = table.Column<int>(type: "integer", nullable: true),
                    ConsentAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastActionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAction = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    HaltReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Watermark = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingAgents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradingAgents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradingAgents_UserId_CreatedAt",
                table: "TradingAgents",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradingAgents");
        }
    }
}
