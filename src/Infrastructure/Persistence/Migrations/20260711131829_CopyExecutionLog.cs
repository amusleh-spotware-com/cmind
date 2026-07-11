using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CopyExecutionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CopyExecutions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationCtidTraderAccountId = table.Column<long>(type: "bigint", nullable: false),
                    SourcePositionId = table.Column<long>(type: "bigint", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsBuy = table.Column<bool>(type: "boolean", nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    MasterPrice = table.Column<double>(type: "double precision", nullable: false),
                    SlippagePoints = table.Column<int>(type: "integer", nullable: true),
                    LatencyMilliseconds = table.Column<double>(type: "double precision", nullable: false),
                    Reason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopyExecutions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CopyExecutions_ProfileId_OccurredAt",
                table: "CopyExecutions",
                columns: new[] { "ProfileId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CopyExecutions");
        }
    }
}
