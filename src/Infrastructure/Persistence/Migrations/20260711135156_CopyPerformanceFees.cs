using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CopyPerformanceFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "HighWaterMarkEquity",
                table: "CopyDestinations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PerformanceFeePercent",
                table: "CopyDestinations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "CopyFeeAccruals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HighWaterMarkBefore = table.Column<double>(type: "double precision", nullable: false),
                    Equity = table.Column<double>(type: "double precision", nullable: false),
                    FeePercent = table.Column<double>(type: "double precision", nullable: false),
                    FeeAmount = table.Column<double>(type: "double precision", nullable: false),
                    SettledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopyFeeAccruals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CopyFeeAccruals_UserId_SettledAt",
                table: "CopyFeeAccruals",
                columns: new[] { "UserId", "SettledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CopyFeeAccruals");

            migrationBuilder.DropColumn(
                name: "HighWaterMarkEquity",
                table: "CopyDestinations");

            migrationBuilder.DropColumn(
                name: "PerformanceFeePercent",
                table: "CopyDestinations");
        }
    }
}
