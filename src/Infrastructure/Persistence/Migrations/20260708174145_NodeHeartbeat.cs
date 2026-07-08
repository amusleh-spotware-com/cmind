using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NodeHeartbeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReachable",
                table: "Nodes",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastHeartbeatAt",
                table: "Nodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Nodes\" SET \"IsReachable\" = true " +
                "WHERE \"IsReachable\" IS NULL AND \"Kind\" <> 'Local';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReachable",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "LastHeartbeatAt",
                table: "Nodes");
        }
    }
}
