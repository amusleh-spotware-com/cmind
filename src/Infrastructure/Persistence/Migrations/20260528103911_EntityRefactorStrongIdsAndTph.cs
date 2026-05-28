using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EntityRefactorStrongIdsAndTph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ViewerSeeAllInstances",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Instances");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "ViewerGrants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ViewerGrants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<bool>(
                name: "SeeAllInstances",
                table: "Users",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Nodes",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompletedBacktestInstance_ContainerId",
                table: "Instances",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedBacktestInstance_StartedAt",
                table: "Instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedBacktestInstance_StoppedAt",
                table: "Instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailedBacktestInstance_ContainerId",
                table: "Instances",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailedBacktestInstance_FailureReason",
                table: "Instances",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FailedBacktestInstance_StartedAt",
                table: "Instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FailedBacktestInstance_StoppedAt",
                table: "Instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Instances",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RunningBacktestInstance_ContainerId",
                table: "Instances",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RunningBacktestInstance_StartedAt",
                table: "Instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RunningRunInstance_ContainerId",
                table: "Instances",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RunningRunInstance_StartedAt",
                table: "Instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartingBacktestInstance_ContainerId",
                table: "Instances",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartingRunInstance_ContainerId",
                table: "Instances",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoppedRunInstance_ContainerId",
                table: "Instances",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StoppedRunInstance_StartedAt",
                table: "Instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StoppedRunInstance_StoppedAt",
                table: "Instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoppingBacktestInstance_ContainerId",
                table: "Instances",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StoppingBacktestInstance_StartedAt",
                table: "Instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoppingRunInstance_ContainerId",
                table: "Instances",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StoppingRunInstance_StartedAt",
                table: "Instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "InstanceLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "InstanceLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "CBotSourceProjects",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ViewerGrants");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ViewerGrants");

            migrationBuilder.DropColumn(
                name: "SeeAllInstances",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "CompletedBacktestInstance_ContainerId",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "CompletedBacktestInstance_StartedAt",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "CompletedBacktestInstance_StoppedAt",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "FailedBacktestInstance_ContainerId",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "FailedBacktestInstance_FailureReason",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "FailedBacktestInstance_StartedAt",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "FailedBacktestInstance_StoppedAt",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "RunningBacktestInstance_ContainerId",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "RunningBacktestInstance_StartedAt",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "RunningRunInstance_ContainerId",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "RunningRunInstance_StartedAt",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "StartingBacktestInstance_ContainerId",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "StartingRunInstance_ContainerId",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "StoppedRunInstance_ContainerId",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "StoppedRunInstance_StartedAt",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "StoppedRunInstance_StoppedAt",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "StoppingBacktestInstance_ContainerId",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "StoppingBacktestInstance_StartedAt",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "StoppingRunInstance_ContainerId",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "StoppingRunInstance_StartedAt",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "InstanceLogs");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "InstanceLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8);

            migrationBuilder.AddColumn<bool>(
                name: "ViewerSeeAllInstances",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "Nodes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Nodes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Instances",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Instances",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "CBotSourceProjects",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(21)",
                oldMaxLength: 21);
        }
    }
}
