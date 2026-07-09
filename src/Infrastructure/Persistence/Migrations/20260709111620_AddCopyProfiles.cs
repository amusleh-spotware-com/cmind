using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCopyProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CopyProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopyProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CopyProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CopyDestinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    RiskMode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RiskParameter = table.Column<double>(type: "double precision", nullable: false),
                    SlippagePips = table.Column<double>(type: "double precision", nullable: false),
                    MaxDelaySeconds = table.Column<int>(type: "integer", nullable: false),
                    Reverse = table.Column<bool>(type: "boolean", nullable: false),
                    CopyStopLoss = table.Column<bool>(type: "boolean", nullable: false),
                    CopyTakeProfit = table.Column<bool>(type: "boolean", nullable: false),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MinLot = table.Column<double>(type: "double precision", nullable: false),
                    MaxLot = table.Column<double>(type: "double precision", nullable: false),
                    ForceMinLot = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDrawdownPercent = table.Column<double>(type: "double precision", nullable: false),
                    DailyLossLimit = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SymbolMaps = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopyDestinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CopyDestinations_CopyProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "CopyProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CopyDestinations_ProfileId_DestinationAccountId",
                table: "CopyDestinations",
                columns: new[] { "ProfileId", "DestinationAccountId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CopyProfiles_UserId_Name",
                table: "CopyProfiles",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CopyDestinations");

            migrationBuilder.DropTable(
                name: "CopyProfiles");
        }
    }
}
