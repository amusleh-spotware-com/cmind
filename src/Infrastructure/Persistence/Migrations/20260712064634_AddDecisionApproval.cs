using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivationState",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Profile_AgeConfirmed",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Profile_Company",
                table: "Users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Profile_CountryCode",
                table: "Users",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Profile_DisplayName",
                table: "Users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Profile_FullName",
                table: "Users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Profile_Locale",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Profile_MarketingOptIn",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Profile_PhoneNumber",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Approval",
                table: "AgentDecisionRecords",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "EmailVerificationToken",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerificationToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailVerificationToken_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDashboards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Widgets = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDashboards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDashboards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ActivationState",
                table: "Users",
                column: "ActivationState");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationToken_TokenHash",
                table: "EmailVerificationToken",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationToken_UserId",
                table: "EmailVerificationToken",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDashboards_UserId",
                table: "UserDashboards",
                column: "UserId",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailVerificationToken");

            migrationBuilder.DropTable(
                name: "UserDashboards");

            migrationBuilder.DropIndex(
                name: "IX_Users_ActivationState",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ActivationState",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Profile_AgeConfirmed",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Profile_Company",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Profile_CountryCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Profile_DisplayName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Profile_FullName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Profile_Locale",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Profile_MarketingOptIn",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Profile_PhoneNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Approval",
                table: "AgentDecisionRecords");
        }
    }
}
