using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    DetailsJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Host = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SshPort = table.Column<int>(type: "integer", nullable: false),
                    SshUser = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EncryptedSshKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    EncryptedSshKeyPassphrase = table.Column<byte[]>(type: "bytea", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DataDirPath = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MaxInstances = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    IsLockedOut = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false),
                    ViewerSeeAllInstances = table.Column<bool>(type: "boolean", nullable: false),
                    MfaSecret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MfaEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SecurityStamp = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NodeStats",
                columns: table => new
                {
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CpuPercent = table.Column<double>(type: "double precision", nullable: false),
                    MemUsedBytes = table.Column<long>(type: "bigint", nullable: false),
                    MemTotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    DiskUsedBytes = table.Column<long>(type: "bigint", nullable: false),
                    DiskTotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    BacktestDataUsedBytes = table.Column<long>(type: "bigint", nullable: false),
                    RunningCount = table.Column<int>(type: "integer", nullable: false),
                    BacktestCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeStats", x => x.NodeId);
                    table.ForeignKey(
                        name: "FK_NodeStats_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CBotSourceProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProjectFilesJson = table.Column<string>(type: "text", nullable: false),
                    LastBuildLog = table.Column<string>(type: "text", nullable: true),
                    LastBuildAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastBuildSucceeded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CBotSourceProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CBotSourceProjects_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CTids",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EncryptedPassword = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CTids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CTids_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "McpApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpApiKeys_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CBots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EncryptedAlgo = table.Column<byte[]>(type: "bytea", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SourceProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CBots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CBots_CBotSourceProjects_SourceProjectId",
                        column: x => x.SourceProjectId,
                        principalTable: "CBotSourceProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CBots_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradingAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CTidId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountNumber = table.Column<long>(type: "bigint", nullable: false),
                    Broker = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsLive = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptedToken = table.Column<byte[]>(type: "bytea", nullable: true),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradingAccounts_CTids_CTidId",
                        column: x => x.CTidId,
                        principalTable: "CTids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParamSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CBotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    JsonContent = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParamSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParamSets_CBots_CBotId",
                        column: x => x.CBotId,
                        principalTable: "CBots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParamSets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Instances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CBotId = table.Column<Guid>(type: "uuid", nullable: false),
                    TradingAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DockerImageTag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Timeframe = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ParamSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    BacktestSettingsJson = table.Column<string>(type: "text", nullable: true),
                    ResultJsonPath = table.Column<string>(type: "text", nullable: true),
                    DataDirSubPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StoppedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Instances_CBots_CBotId",
                        column: x => x.CBotId,
                        principalTable: "CBots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Instances_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Instances_ParamSets_ParamSetId",
                        column: x => x.ParamSetId,
                        principalTable: "ParamSets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Instances_TradingAccounts_TradingAccountId",
                        column: x => x.TradingAccountId,
                        principalTable: "TradingAccounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Instances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstanceLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Stream = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Line = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstanceLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstanceLogs_Instances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "Instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ViewerGrants",
                columns: table => new
                {
                    ViewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewerGrants", x => new { x.ViewerId, x.InstanceId });
                    table.ForeignKey(
                        name: "FK_ViewerGrants_Instances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "Instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ViewerGrants_Users_ViewerId",
                        column: x => x.ViewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_Ts",
                table: "AuditLogs",
                columns: new[] { "UserId", "Ts" });

            migrationBuilder.CreateIndex(
                name: "IX_CBotSourceProjects_UserId_Name",
                table: "CBotSourceProjects",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CBots_SourceProjectId",
                table: "CBots",
                column: "SourceProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CBots_UserId_Name",
                table: "CBots",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CTids_UserId_Username",
                table: "CTids",
                columns: new[] { "UserId", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstanceLogs_InstanceId_Ts",
                table: "InstanceLogs",
                columns: new[] { "InstanceId", "Ts" });

            migrationBuilder.CreateIndex(
                name: "IX_Instances_CBotId",
                table: "Instances",
                column: "CBotId");

            migrationBuilder.CreateIndex(
                name: "IX_Instances_NodeId_Status",
                table: "Instances",
                columns: new[] { "NodeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Instances_ParamSetId",
                table: "Instances",
                column: "ParamSetId");

            migrationBuilder.CreateIndex(
                name: "IX_Instances_TradingAccountId",
                table: "Instances",
                column: "TradingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Instances_UserId_CreatedAt",
                table: "Instances",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_McpApiKeys_KeyPrefix",
                table: "McpApiKeys",
                column: "KeyPrefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpApiKeys_UserId",
                table: "McpApiKeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_Name",
                table: "Nodes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParamSets_CBotId_Name",
                table: "ParamSets",
                columns: new[] { "CBotId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParamSets_UserId",
                table: "ParamSets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TradingAccounts_CTidId_AccountNumber",
                table: "TradingAccounts",
                columns: new[] { "CTidId", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ViewerGrants_InstanceId",
                table: "ViewerGrants",
                column: "InstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "InstanceLogs");

            migrationBuilder.DropTable(
                name: "McpApiKeys");

            migrationBuilder.DropTable(
                name: "NodeStats");

            migrationBuilder.DropTable(
                name: "ViewerGrants");

            migrationBuilder.DropTable(
                name: "Instances");

            migrationBuilder.DropTable(
                name: "Nodes");

            migrationBuilder.DropTable(
                name: "ParamSets");

            migrationBuilder.DropTable(
                name: "TradingAccounts");

            migrationBuilder.DropTable(
                name: "CBots");

            migrationBuilder.DropTable(
                name: "CTids");

            migrationBuilder.DropTable(
                name: "CBotSourceProjects");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
