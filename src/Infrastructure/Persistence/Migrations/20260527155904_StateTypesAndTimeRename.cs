using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StateTypesAndTimeRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Instances_NodeId_Status",
                table: "Instances");

            migrationBuilder.RenameColumn(
                name: "Ts",
                table: "InstanceLogs",
                newName: "Time");

            migrationBuilder.RenameIndex(
                name: "IX_InstanceLogs_InstanceId_Ts",
                table: "InstanceLogs",
                newName: "IX_InstanceLogs_InstanceId_Time");

            migrationBuilder.RenameColumn(
                name: "Ts",
                table: "AuditLogs",
                newName: "Time");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_UserId_Ts",
                table: "AuditLogs",
                newName: "IX_AuditLogs_UserId_Time");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Nodes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Mode",
                table: "Nodes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Instances",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Instances",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "CBotSourceProjects",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_Instances_NodeId",
                table: "Instances",
                column: "NodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Instances_NodeId",
                table: "Instances");

            migrationBuilder.RenameColumn(
                name: "Time",
                table: "InstanceLogs",
                newName: "Ts");

            migrationBuilder.RenameIndex(
                name: "IX_InstanceLogs_InstanceId_Time",
                table: "InstanceLogs",
                newName: "IX_InstanceLogs_InstanceId_Ts");

            migrationBuilder.RenameColumn(
                name: "Time",
                table: "AuditLogs",
                newName: "Ts");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_UserId_Time",
                table: "AuditLogs",
                newName: "IX_AuditLogs_UserId_Ts");

            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "Users",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Nodes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<int>(
                name: "Mode",
                table: "Nodes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Instances",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Instances",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<int>(
                name: "Language",
                table: "CBotSourceProjects",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.CreateIndex(
                name: "IX_Instances_NodeId_Status",
                table: "Instances",
                columns: new[] { "NodeId", "Status" });
        }
    }
}
