using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstanceLineageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill each existing instance with its OWN unique lineage id (not a shared empty Guid) so a
            // detail page polling by LineageId never resolves the wrong historical instance.
            migrationBuilder.AddColumn<Guid>(
                name: "LineageId",
                table: "Instances",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineageId",
                table: "Instances");
        }
    }
}
