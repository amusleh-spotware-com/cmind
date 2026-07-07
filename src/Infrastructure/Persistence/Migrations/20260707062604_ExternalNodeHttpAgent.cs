using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExternalNodeHttpAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptedSshKey",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "Host",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "SshPort",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "SshUser",
                table: "Nodes");

            migrationBuilder.RenameColumn(
                name: "EncryptedSshKeyPassphrase",
                table: "Nodes",
                newName: "EncryptedApiSecret");

            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "Nodes",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "Nodes");

            migrationBuilder.RenameColumn(
                name: "EncryptedApiSecret",
                table: "Nodes",
                newName: "EncryptedSshKeyPassphrase");

            migrationBuilder.AddColumn<byte[]>(
                name: "EncryptedSshKey",
                table: "Nodes",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Host",
                table: "Nodes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SshPort",
                table: "Nodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SshUser",
                table: "Nodes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }
    }
}
