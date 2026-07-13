using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "calendar");

            migrationBuilder.CreateTable(
                name: "AiProviderCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Model = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EncryptedApiKey = table.Column<byte[]>(type: "bytea", nullable: true),
                    MaxTokens = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsWebSearch = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsVision = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsSystemRole = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsTools = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviderCredentials", x => x.Id);
                });

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
                    Time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    PrevHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "CopyNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationCtidTraderAccountId = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Acknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopyNotifications", x => x.Id);
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
                name: "LegalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DataDirPath = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MaxInstances = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    EncryptedApiSecret = table.Column<byte[]>(type: "bytea", nullable: true),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsReachable = table.Column<bool>(type: "boolean", nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: true),
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
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    IsLockedOut = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EncryptedMfaSecret = table.Column<byte[]>(type: "bytea", nullable: true),
                    MfaEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SecurityStamp = table.Column<byte[]>(type: "bytea", nullable: false),
                    ActivationState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Profile_FullName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Profile_DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Profile_CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Profile_PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Profile_Company = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Profile_Locale = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Profile_MarketingOptIn = table.Column<bool>(type: "boolean", nullable: false),
                    Profile_AgeConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    Role = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SeeAllInstances = table.Column<bool>(type: "boolean", nullable: true),
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
                name: "currency_strength_snapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOf = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RankingJson = table.Column<string>(type: "jsonb", nullable: false),
                    HorizonsJson = table.Column<string>(type: "jsonb", nullable: false),
                    IndicatorsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Narrative = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CalendarKnownAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currency_strength_snapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "economic_event",
                schema: "calendar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesCodeValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CountryValue = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Precision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceTimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Released = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_economic_event", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "series",
                schema: "calendar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesCodeValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CountryValue = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Cadence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DefaultImpact = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ImpactPrior = table.Column<double>(type: "double precision", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceSeriesId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_series", x => x.Id);
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
                name: "AgentDecisionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Reasoning = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    OrderJson = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    EvidenceCsv = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ShouldExecute = table.Column<bool>(type: "boolean", nullable: false),
                    ShouldHalt = table.Column<bool>(type: "boolean", nullable: false),
                    Executed = table.Column<bool>(type: "boolean", nullable: false),
                    Approval = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentDecisionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentDecisionRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentMemories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tier = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMemories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentMemories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastEvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Trigger = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    MinImpactLevel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    MinutesBefore = table.Column<int>(type: "integer", nullable: true),
                    Currencies = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastTriggeredEventKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertRules_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CBotSourceProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EncryptedProjectFiles = table.Column<byte[]>(type: "bytea", nullable: false),
                    LastBuildLog = table.Column<string>(type: "text", nullable: true),
                    LastBuildAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastBuildSucceeded = table.Column<bool>(type: "boolean", nullable: false),
                    Language = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
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
                    CtidUserId = table.Column<long>(type: "bigint", nullable: true),
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
                name: "ConsentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CopyProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AssignedNode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FlattenRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopyProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CopyProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CopyProviderListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PerformanceFeePercent = table.Column<double>(type: "double precision", nullable: false),
                    VerifiedLive = table.Column<bool>(type: "boolean", nullable: false),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopyProviderListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CopyProviderListings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "JournalNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalNotes_Users_UserId",
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
                name: "MfaBackupCode",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaBackupCode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MfaBackupCode_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenApiApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EncryptedClientSecret = table.Column<byte[]>(type: "bytea", nullable: false),
                    RedirectUri = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenApiApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenApiApplications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropFirmChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TradingAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StartingBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    ProfitTargetPercent = table.Column<double>(type: "double precision", nullable: false),
                    MaxDailyLossPercent = table.Column<double>(type: "double precision", nullable: false),
                    MaxTotalDrawdownPercent = table.Column<double>(type: "double precision", nullable: false),
                    DrawdownMode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    MinTradingDays = table.Column<int>(type: "integer", nullable: false),
                    SingleStep = table.Column<bool>(type: "boolean", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DailyLossBasis = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TrailingThresholdAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TrailingLockThreshold = table.Column<decimal>(type: "numeric", nullable: false),
                    ConsistencyMaxDayProfitSharePercent = table.Column<double>(type: "double precision", nullable: true),
                    MaxCalendarDays = table.Column<int>(type: "integer", nullable: true),
                    MaxInactivityDays = table.Column<int>(type: "integer", nullable: true),
                    MaxOpenPositions = table.Column<int>(type: "integer", nullable: true),
                    AllowWeekendHolding = table.Column<bool>(type: "boolean", nullable: false),
                    AllowNewsTrading = table.Column<bool>(type: "boolean", nullable: false),
                    DrawdownWarnThresholdPercent = table.Column<double>(type: "double precision", nullable: false),
                    Phase = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Breach = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CurrentEquity = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    PeakEquity = table.Column<decimal>(type: "numeric", nullable: false),
                    DailyStartEquity = table.Column<decimal>(type: "numeric", nullable: false),
                    DailyStartBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxSingleDayProfit = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrentDay = table.Column<DateOnly>(type: "date", nullable: true),
                    TradingDaysCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastEquityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DrawdownWarned = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedNode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropFirmChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropFirmChallenges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradingAgents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Archetype = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Autonomy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Aggressiveness = table.Column<double>(type: "double precision", nullable: false),
                    Patience = table.Column<double>(type: "double precision", nullable: false),
                    TrendBias = table.Column<double>(type: "double precision", nullable: false),
                    ObjectiveDrawdownWeight = table.Column<double>(type: "double precision", nullable: false),
                    EnvMaxDailyLossPercent = table.Column<double>(type: "double precision", nullable: true),
                    EnvMaxOpenExposureLots = table.Column<double>(type: "double precision", nullable: true),
                    EnvMaxPositionSizeLots = table.Column<double>(type: "double precision", nullable: true),
                    EnvMaxLeverage = table.Column<double>(type: "double precision", nullable: true),
                    EnvMaxConsecutiveLosses = table.Column<int>(type: "integer", nullable: true),
                    EnvMaxOrdersPerHour = table.Column<int>(type: "integer", nullable: true),
                    EnvAllowedSymbolsCsv = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    GoalsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ManagedAccountsCsv = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ConsentVersion = table.Column<int>(type: "integer", nullable: true),
                    ConsentAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastActionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAction = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    HaltReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Watermark = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingAgents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradingAgents_Users_UserId",
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

            migrationBuilder.CreateTable(
                name: "api_client",
                schema: "calendar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScopesCsv = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DisabledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_client", x => x.Id);
                    table.ForeignKey(
                        name: "FK_api_client_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webhook",
                schema: "calendar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    EncryptedSecret = table.Column<byte[]>(type: "bytea", nullable: false),
                    MinImpactLevel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Currencies = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DisabledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook", x => x.Id);
                    table.ForeignKey(
                        name: "FK_webhook_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_revision",
                schema: "calendar",
                columns: table => new
                {
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CalendarEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnownAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Actual = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Forecast = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Previous = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ImpactScore = table.Column<double>(type: "double precision", nullable: false),
                    ImpactLevel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ImpactModelVersion = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SourceRef = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RescheduledInstant = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_revision", x => new { x.CalendarEventId, x.Sequence });
                    table.ForeignKey(
                        name: "FK_event_revision_economic_event_CalendarEventId",
                        column: x => x.CalendarEventId,
                        principalSchema: "calendar",
                        principalTable: "economic_event",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Acknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertEvents_AlertRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "AlertRules",
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
                    LinkMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CtidTraderAccountId = table.Column<long>(type: "bigint", nullable: true),
                    OpenApiAuthorizationId = table.Column<Guid>(type: "uuid", nullable: true),
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
                name: "CopyDestinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    RiskMode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RiskParameter = table.Column<double>(type: "double precision", nullable: false),
                    SlippagePips = table.Column<double>(type: "double precision", nullable: false),
                    MaxDelaySeconds = table.Column<int>(type: "integer", nullable: false),
                    Reverse = table.Column<bool>(type: "boolean", nullable: false),
                    CopyStopLoss = table.Column<bool>(type: "boolean", nullable: false),
                    CopyTakeProfit = table.Column<bool>(type: "boolean", nullable: false),
                    MirrorPartialClose = table.Column<bool>(type: "boolean", nullable: false),
                    MirrorScaleIn = table.Column<bool>(type: "boolean", nullable: false),
                    CopyPendingOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CopyTrailingStop = table.Column<bool>(type: "boolean", nullable: false),
                    CopyOrderTypes = table.Column<int>(type: "integer", nullable: false),
                    CopyPendingExpiry = table.Column<bool>(type: "boolean", nullable: false),
                    CopyMasterSlippage = table.Column<bool>(type: "boolean", nullable: false),
                    ManageOnly = table.Column<bool>(type: "boolean", nullable: false),
                    SyncOpenOnStart = table.Column<bool>(type: "boolean", nullable: false),
                    SyncClosedOnStart = table.Column<bool>(type: "boolean", nullable: false),
                    SourceLabelFilter = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MinLot = table.Column<double>(type: "double precision", nullable: false),
                    MaxLot = table.Column<double>(type: "double precision", nullable: false),
                    ForceMinLot = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDrawdownPercent = table.Column<double>(type: "double precision", nullable: false),
                    DailyLossLimit = table.Column<double>(type: "double precision", nullable: false),
                    RiskFallbackLots = table.Column<double>(type: "double precision", nullable: false),
                    LotSanityAbsoluteMaxLots = table.Column<double>(type: "double precision", nullable: false),
                    LotSanityMasterMultiple = table.Column<double>(type: "double precision", nullable: false),
                    TradingHoursStartMinuteUtc = table.Column<int>(type: "integer", nullable: false),
                    TradingHoursEndMinuteUtc = table.Column<int>(type: "integer", nullable: false),
                    AccountProtectionMode = table.Column<int>(type: "integer", nullable: false),
                    AccountProtectionStopEquity = table.Column<double>(type: "double precision", nullable: false),
                    AccountProtectionTakeEquity = table.Column<double>(type: "double precision", nullable: true),
                    PropRuleDailyLossCap = table.Column<double>(type: "double precision", nullable: false),
                    PropRuleTrailingDrawdown = table.Column<double>(type: "double precision", nullable: false),
                    ConsistencyThresholdPercent = table.Column<double>(type: "double precision", nullable: false),
                    ConfigLockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExecutionJitterMaxMs = table.Column<int>(type: "integer", nullable: false),
                    PerformanceFeePercent = table.Column<double>(type: "double precision", nullable: false),
                    HighWaterMarkEquity = table.Column<double>(type: "double precision", nullable: false),
                    SymbolFilterMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SymbolFilters = table.Column<string>(type: "jsonb", nullable: true),
                    SymbolMaps = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopyDestinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CopyDestinations_CopyProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "CopyProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenApiAuthorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CtidUserId = table.Column<long>(type: "bigint", nullable: false),
                    IsLive = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptedAccessToken = table.Column<byte[]>(type: "bytea", nullable: false),
                    EncryptedRefreshToken = table.Column<byte[]>(type: "bytea", nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LastRefreshedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RefreshFailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsecutiveRefreshFailures = table.Column<int>(type: "integer", nullable: false),
                    RefreshCriticalAlerted = table.Column<bool>(type: "boolean", nullable: false),
                    TokenVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenApiAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenApiAuthorizations_OpenApiApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "OpenApiApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OpenApiAuthorizations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
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
                name: "AgentMandates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CBotId = table.Column<Guid>(type: "uuid", nullable: false),
                    TradingAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Objective = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    RiskPercentPerTrade = table.Column<double>(type: "double precision", nullable: false),
                    MaxDrawdownPercent = table.Column<double>(type: "double precision", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Timeframe = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DockerImageTag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BacktestSettingsJson = table.Column<string>(type: "text", nullable: true),
                    Autonomy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMandates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentMandates_CBots_CBotId",
                        column: x => x.CBotId,
                        principalTable: "CBots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentMandates_TradingAccounts_TradingAccountId",
                        column: x => x.TradingAccountId,
                        principalTable: "TradingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AgentMandates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TradingAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MaxConcurrentLiveInstances = table.Column<int>(type: "integer", nullable: false),
                    DailyLossLimit = table.Column<double>(type: "double precision", nullable: false),
                    MaxDrawdownPercent = table.Column<double>(type: "double precision", nullable: false),
                    AutoFlatten = table.Column<bool>(type: "boolean", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastFlattenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropRules_TradingAccounts_TradingAccountId",
                        column: x => x.TradingAccountId,
                        principalTable: "TradingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropRules_Users_UserId",
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
                    DockerImageTag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Timeframe = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ParamSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    DataDirSubPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Kind = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    CompletedBacktestInstance_ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CompletedBacktestInstance_StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBacktestInstance_StoppedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultJsonPath = table.Column<string>(type: "text", nullable: true),
                    ReportJson = table.Column<string>(type: "text", nullable: true),
                    BacktestSettingsJson = table.Column<string>(type: "text", nullable: true),
                    FailedBacktestInstance_ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailedBacktestInstance_StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedBacktestInstance_StoppedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedBacktestInstance_FailureReason = table.Column<string>(type: "text", nullable: true),
                    ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StoppedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    RunningBacktestInstance_ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RunningBacktestInstance_StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RunningRunInstance_ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RunningRunInstance_StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartingBacktestInstance_ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StartingRunInstance_ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StoppedRunInstance_ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StoppedRunInstance_StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StoppedRunInstance_StoppedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StoppingBacktestInstance_ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StoppingBacktestInstance_StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StoppingRunInstance_ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StoppingRunInstance_StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                name: "AgentProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MandateId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reasoning = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    ProposedName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedParamSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentProposals_AgentMandates_MandateId",
                        column: x => x.MandateId,
                        principalTable: "AgentMandates",
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
                    Time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Stream = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Line = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                name: "IX_AgentDecisionRecords_AgentId_Sequence",
                table: "AgentDecisionRecords",
                columns: new[] { "AgentId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentDecisionRecords_UserId",
                table: "AgentDecisionRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMandates_CBotId",
                table: "AgentMandates",
                column: "CBotId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMandates_TradingAccountId",
                table: "AgentMandates",
                column: "TradingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMandates_UserId_Name",
                table: "AgentMandates",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMemories_AgentId_CreatedAt",
                table: "AgentMemories",
                columns: new[] { "AgentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentMemories_UserId",
                table: "AgentMemories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentProposals_MandateId_CreatedAt",
                table: "AgentProposals",
                columns: new[] { "MandateId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderCredentials_IsActive",
                table: "AiProviderCredentials",
                column: "IsActive",
                unique: true,
                filter: "\"OwnerUserId\" IS NULL AND \"IsActive\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderCredentials_OwnerUserId_IsActive",
                table: "AiProviderCredentials",
                columns: new[] { "OwnerUserId", "IsActive" },
                unique: true,
                filter: "\"OwnerUserId\" IS NOT NULL AND \"IsActive\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_RuleId",
                table: "AlertEvents",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_UserId_CreatedAt",
                table: "AlertEvents",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_UserId_Name",
                table: "AlertRules",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_Time",
                table: "AuditLogs",
                columns: new[] { "UserId", "Time" });

            migrationBuilder.CreateIndex(
                name: "IX_CBotSourceProjects_UserId_Name",
                table: "CBotSourceProjects",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CBots_SourceProjectId",
                table: "CBots",
                column: "SourceProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CBots_UserId_Name",
                table: "CBots",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CTids_UserId_Username",
                table: "CTids",
                columns: new[] { "UserId", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_UserId_DocumentType_Version",
                table: "ConsentRecords",
                columns: new[] { "UserId", "DocumentType", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_CopyDestinations_ProfileId_DestinationAccountId",
                table: "CopyDestinations",
                columns: new[] { "ProfileId", "DestinationAccountId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CopyExecutions_ProfileId_OccurredAt",
                table: "CopyExecutions",
                columns: new[] { "ProfileId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CopyFeeAccruals_UserId_SettledAt",
                table: "CopyFeeAccruals",
                columns: new[] { "UserId", "SettledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CopyNotifications_UserId_OccurredAt",
                table: "CopyNotifications",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CopyProfiles_UserId_Name",
                table: "CopyProfiles",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CopyProviderListings_ProfileId",
                table: "CopyProviderListings",
                column: "ProfileId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CopyProviderListings_Published",
                table: "CopyProviderListings",
                column: "Published");

            migrationBuilder.CreateIndex(
                name: "IX_CopyProviderListings_UserId",
                table: "CopyProviderListings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationToken_TokenHash",
                table: "EmailVerificationToken",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationToken_UserId",
                table: "EmailVerificationToken",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InstanceLogs_InstanceId_Time",
                table: "InstanceLogs",
                columns: new[] { "InstanceId", "Time" });

            migrationBuilder.CreateIndex(
                name: "IX_Instances_CBotId",
                table: "Instances",
                column: "CBotId");

            migrationBuilder.CreateIndex(
                name: "IX_Instances_NodeId",
                table: "Instances",
                column: "NodeId");

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
                name: "IX_JournalNotes_UserId_CreatedAt",
                table: "JournalNotes",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_Type_Version",
                table: "LegalDocuments",
                columns: new[] { "Type", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = false");

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
                name: "IX_MfaBackupCode_UserId_CodeHash",
                table: "MfaBackupCode",
                columns: new[] { "UserId", "CodeHash" });

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_Name",
                table: "Nodes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenApiApplications_IsShared",
                table: "OpenApiApplications",
                column: "IsShared",
                unique: true,
                filter: "\"IsShared\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_OpenApiApplications_UserId",
                table: "OpenApiApplications",
                column: "UserId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_OpenApiAuthorizations_ApplicationId",
                table: "OpenApiAuthorizations",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenApiAuthorizations_UserId_CtidUserId",
                table: "OpenApiAuthorizations",
                columns: new[] { "UserId", "CtidUserId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

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
                name: "IX_PropFirmChallenges_UserId_CreatedAt",
                table: "PropFirmChallenges",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PropRules_TradingAccountId",
                table: "PropRules",
                column: "TradingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PropRules_UserId_TradingAccountId",
                table: "PropRules",
                columns: new[] { "UserId", "TradingAccountId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_TradingAccounts_CTidId_AccountNumber",
                table: "TradingAccounts",
                columns: new[] { "CTidId", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradingAgents_UserId_CreatedAt",
                table: "TradingAgents",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDashboards_UserId",
                table: "UserDashboards",
                column: "UserId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ActivationState",
                table: "Users",
                column: "ActivationState");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ViewerGrants_InstanceId",
                table: "ViewerGrants",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_api_client_KeyPrefix",
                schema: "calendar",
                table: "api_client",
                column: "KeyPrefix",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_api_client_OwnerId",
                schema: "calendar",
                table: "api_client",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_currency_strength_snapshot_AsOf",
                table: "currency_strength_snapshot",
                column: "AsOf");

            migrationBuilder.CreateIndex(
                name: "IX_economic_event_EffectiveAt",
                schema: "calendar",
                table: "economic_event",
                column: "EffectiveAt");

            migrationBuilder.CreateIndex(
                name: "IX_economic_event_SeriesId_EffectiveAt",
                schema: "calendar",
                table: "economic_event",
                columns: new[] { "SeriesId", "EffectiveAt" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_event_revision_CalendarEventId_KnownAt",
                schema: "calendar",
                table: "event_revision",
                columns: new[] { "CalendarEventId", "KnownAt" });

            migrationBuilder.CreateIndex(
                name: "IX_series_SeriesCodeValue",
                schema: "calendar",
                table: "series",
                column: "SeriesCodeValue",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_OwnerId",
                schema: "calendar",
                table: "webhook",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentDecisionRecords");

            migrationBuilder.DropTable(
                name: "AgentMemories");

            migrationBuilder.DropTable(
                name: "AgentProposals");

            migrationBuilder.DropTable(
                name: "AiProviderCredentials");

            migrationBuilder.DropTable(
                name: "AlertEvents");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ConsentRecords");

            migrationBuilder.DropTable(
                name: "CopyDestinations");

            migrationBuilder.DropTable(
                name: "CopyExecutions");

            migrationBuilder.DropTable(
                name: "CopyFeeAccruals");

            migrationBuilder.DropTable(
                name: "CopyNotifications");

            migrationBuilder.DropTable(
                name: "CopyProviderListings");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "EmailVerificationToken");

            migrationBuilder.DropTable(
                name: "InstanceLogs");

            migrationBuilder.DropTable(
                name: "JournalNotes");

            migrationBuilder.DropTable(
                name: "LegalDocuments");

            migrationBuilder.DropTable(
                name: "McpApiKeys");

            migrationBuilder.DropTable(
                name: "MfaBackupCode");

            migrationBuilder.DropTable(
                name: "NodeStats");

            migrationBuilder.DropTable(
                name: "OpenApiAuthorizations");

            migrationBuilder.DropTable(
                name: "PropFirmChallenges");

            migrationBuilder.DropTable(
                name: "PropRules");

            migrationBuilder.DropTable(
                name: "TradingAgents");

            migrationBuilder.DropTable(
                name: "UserDashboards");

            migrationBuilder.DropTable(
                name: "ViewerGrants");

            migrationBuilder.DropTable(
                name: "api_client",
                schema: "calendar");

            migrationBuilder.DropTable(
                name: "currency_strength_snapshot");

            migrationBuilder.DropTable(
                name: "event_revision",
                schema: "calendar");

            migrationBuilder.DropTable(
                name: "series",
                schema: "calendar");

            migrationBuilder.DropTable(
                name: "webhook",
                schema: "calendar");

            migrationBuilder.DropTable(
                name: "AgentMandates");

            migrationBuilder.DropTable(
                name: "AlertRules");

            migrationBuilder.DropTable(
                name: "CopyProfiles");

            migrationBuilder.DropTable(
                name: "OpenApiApplications");

            migrationBuilder.DropTable(
                name: "Instances");

            migrationBuilder.DropTable(
                name: "economic_event",
                schema: "calendar");

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
