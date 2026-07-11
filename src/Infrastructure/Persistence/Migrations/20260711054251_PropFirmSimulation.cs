using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PropFirmSimulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DrawdownMode",
                table: "PropFirmChallenges",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "Breach",
                table: "PropFirmChallenges",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AddColumn<bool>(
                name: "AllowNewsTrading",
                table: "PropFirmChallenges",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowWeekendHolding",
                table: "PropFirmChallenges",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AssignedNode",
                table: "PropFirmChallenges",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ConsistencyMaxDayProfitSharePercent",
                table: "PropFirmChallenges",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentBalance",
                table: "PropFirmChallenges",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DailyLossBasis",
                table: "PropFirmChallenges",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DailyStartBalance",
                table: "PropFirmChallenges",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "DrawdownWarnThresholdPercent",
                table: "PropFirmChallenges",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "DrawdownWarned",
                table: "PropFirmChallenges",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "PropFirmChallenges",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActivityAt",
                table: "PropFirmChallenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAt",
                table: "PropFirmChallenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxCalendarDays",
                table: "PropFirmChallenges",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxInactivityDays",
                table: "PropFirmChallenges",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxOpenPositions",
                table: "PropFirmChallenges",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxSingleDayProfit",
                table: "PropFirmChallenges",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "PropFirmChallenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TrailingLockThreshold",
                table: "PropFirmChallenges",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TrailingThresholdAmount",
                table: "PropFirmChallenges",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowNewsTrading",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "AllowWeekendHolding",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "AssignedNode",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "ConsistencyMaxDayProfitSharePercent",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "CurrentBalance",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "DailyLossBasis",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "DailyStartBalance",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "DrawdownWarnThresholdPercent",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "DrawdownWarned",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "MaxCalendarDays",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "MaxInactivityDays",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "MaxOpenPositions",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "MaxSingleDayProfit",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "TrailingLockThreshold",
                table: "PropFirmChallenges");

            migrationBuilder.DropColumn(
                name: "TrailingThresholdAmount",
                table: "PropFirmChallenges");

            migrationBuilder.AlterColumn<string>(
                name: "DrawdownMode",
                table: "PropFirmChallenges",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldMaxLength: 24);

            migrationBuilder.AlterColumn<string>(
                name: "Breach",
                table: "PropFirmChallenges",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldMaxLength: 24);
        }
    }
}
