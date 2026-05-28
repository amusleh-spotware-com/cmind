using System.ComponentModel.DataAnnotations;

namespace Core;

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}

public abstract class AuditedEntity<TId> : ISoftDeletable
    where TId : struct, IStronglyTypedId<TId>
{
    public TId Id { get; set; } = TId.New();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

// ---------------- AppUser hierarchy by role ----------------

public abstract class AppUser : AuditedEntity<UserId>
{
    [MaxLength(256)] public string Email { get; set; } = default!;
    [MaxLength(256)] public string NormalizedEmail { get; set; } = default!;
    [MaxLength(512)] public string PasswordHash { get; set; } = default!;
    public UserId? CreatedByUserId { get; set; }
    public bool MustChangePassword { get; set; }
    public bool IsLockedOut { get; set; }
    public int AccessFailedCount { get; set; }
    [MaxLength(64)] public string? MfaSecret { get; set; }
    public bool MfaEnabled { get; set; }
    public byte[] SecurityStamp { get; set; } = default!;

    public abstract string RoleName { get; }
    public abstract int RoleRank { get; }
}

public sealed class OwnerUser : AppUser
{
    public override string RoleName => "Owner";
    public override int RoleRank => 0;
}

public sealed class AdminUser : AppUser
{
    public override string RoleName => "Admin";
    public override int RoleRank => 1;
}

public sealed class RegularUser : AppUser
{
    public override string RoleName => "User";
    public override int RoleRank => 2;
}

public sealed class ViewerUser : AppUser
{
    public override string RoleName => "Viewer";
    public override int RoleRank => 3;
    public bool SeeAllInstances { get; set; }
}

// ---------------- cTrader ID ----------------

public class CTraderIdAccount : AuditedEntity<CtidId>
{
    public UserId UserId { get; set; }
    public AppUser User { get; set; } = default!;
    [MaxLength(256)] public string Username { get; set; } = default!;
    public byte[] EncryptedPassword { get; set; } = default!;
    public List<TradingAccount> TradingAccounts { get; set; } = [];
}

public class TradingAccount : AuditedEntity<TradingAccountId>
{
    public CtidId CTidId { get; set; }
    public CTraderIdAccount CTid { get; set; } = default!;
    public long AccountNumber { get; set; }
    [MaxLength(128)] public string Broker { get; set; } = default!;
    public bool IsLive { get; set; }
    public byte[]? EncryptedToken { get; set; }
    [MaxLength(128)] public string? Label { get; set; }
}

// ---------------- CBot ----------------

public class CBot : AuditedEntity<CBotId>
{
    public UserId UserId { get; set; }
    public AppUser User { get; set; } = default!;
    [MaxLength(256)] public string Name { get; set; } = default!;
    public byte[] EncryptedAlgo { get; set; } = default!;
    public int Version { get; set; } = 1;
    public CBotSourceProjectId? SourceProjectId { get; set; }
    public CBotSourceProject? SourceProject { get; set; }
    public List<ParamSet> ParamSets { get; set; } = [];
}

// ---------------- CBotSourceProject hierarchy by language ----------------

public abstract class CBotSourceProject : AuditedEntity<CBotSourceProjectId>
{
    public UserId UserId { get; set; }
    public AppUser User { get; set; } = default!;
    [MaxLength(256)] public string Name { get; set; } = default!;
    public string ProjectFilesJson { get; set; } = "{}";
    public string? LastBuildLog { get; set; }
    public DateTimeOffset? LastBuildAt { get; set; }
    public bool LastBuildSucceeded { get; set; }

    public abstract string LanguageName { get; }
    public abstract string FileExtension { get; }
}

public sealed class CSharpProject : CBotSourceProject
{
    public override string LanguageName => "CSharp";
    public override string FileExtension => ".cs";
}

public sealed class PythonProject : CBotSourceProject
{
    public override string LanguageName => "Python";
    public override string FileExtension => ".py";
}

// ---------------- ParamSet ----------------

public class ParamSet : AuditedEntity<ParamSetId>
{
    public UserId UserId { get; set; }
    public AppUser User { get; set; } = default!;
    public CBotId CBotId { get; set; }
    public CBot CBot { get; set; } = default!;
    [MaxLength(256)] public string Name { get; set; } = default!;
    public string JsonContent { get; set; } = "{}";
}

// ---------------- Node hierarchy by Mode+Status ----------------

public abstract class Node : AuditedEntity<NodeId>
{
    [MaxLength(128)] public string Name { get; set; } = default!;
    [MaxLength(256)] public string Host { get; set; } = default!;
    public int SshPort { get; set; } = 22;
    [MaxLength(64)] public string SshUser { get; set; } = default!;
    public byte[] EncryptedSshKey { get; set; } = default!;
    public byte[]? EncryptedSshKeyPassphrase { get; set; }
    [MaxLength(256)] public string DataDirPath { get; set; } = "/var/ctw/data";
    public int MaxInstances { get; set; } = 10;
    public NodeStats? LatestStats { get; set; }

    public abstract string ModeName { get; }
    public abstract string StatusName { get; }
    public abstract bool IsActive { get; }
    public abstract bool AcceptsRun { get; }
    public abstract bool AcceptsBacktest { get; }
}

public sealed class ActiveRunNode : Node
{
    public override string ModeName => "Run";
    public override string StatusName => "Active";
    public override bool IsActive => true;
    public override bool AcceptsRun => true;
    public override bool AcceptsBacktest => false;
}

public sealed class ActiveBacktestNode : Node
{
    public override string ModeName => "Backtest";
    public override string StatusName => "Active";
    public override bool IsActive => true;
    public override bool AcceptsRun => false;
    public override bool AcceptsBacktest => true;
}

public sealed class ActiveMixedNode : Node
{
    public override string ModeName => "Mixed";
    public override string StatusName => "Active";
    public override bool IsActive => true;
    public override bool AcceptsRun => true;
    public override bool AcceptsBacktest => true;
}

public sealed class DecommissioningNode : Node
{
    public override string ModeName => "Decommissioning";
    public override string StatusName => "Decommissioning";
    public override bool IsActive => false;
    public override bool AcceptsRun => false;
    public override bool AcceptsBacktest => false;
}

public sealed class OfflineNode : Node
{
    public override string ModeName => "Offline";
    public override string StatusName => "Offline";
    public override bool IsActive => false;
    public override bool AcceptsRun => false;
    public override bool AcceptsBacktest => false;
}

public class NodeStats
{
    public NodeId NodeId { get; set; }
    public Node Node { get; set; } = default!;
    public double CpuPercent { get; set; }
    public long MemUsedBytes { get; set; }
    public long MemTotalBytes { get; set; }
    public long DiskUsedBytes { get; set; }
    public long DiskTotalBytes { get; set; }
    public long BacktestDataUsedBytes { get; set; }
    public int RunningCount { get; set; }
    public int BacktestCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// ---------------- Instance hierarchy by Kind + Status ----------------

public abstract class Instance : AuditedEntity<InstanceId>
{
    public UserId UserId { get; set; }
    public AppUser User { get; set; } = default!;
    public CBotId CBotId { get; set; }
    public CBot CBot { get; set; } = default!;
    public TradingAccountId? TradingAccountId { get; set; }
    public TradingAccount? TradingAccount { get; set; }
    public NodeId? NodeId { get; set; }
    public Node? Node { get; set; }
    [MaxLength(128)] public string DockerImageTag { get; set; } = "latest";
    [MaxLength(32)] public string? Symbol { get; set; }
    [MaxLength(16)] public string? Timeframe { get; set; }
    public ParamSetId? ParamSetId { get; set; }
    public ParamSet? ParamSet { get; set; }
    [MaxLength(512)] public string? DataDirSubPath { get; set; }

    public abstract string KindName { get; }
    public abstract string StatusName { get; }
    public abstract bool IsTerminal { get; }
    public abstract bool IsActive { get; }
}

// Run instances

public abstract class RunInstance : Instance
{
    public sealed override string KindName => "Run";
}

public sealed class PendingRunInstance : RunInstance
{
    public override string StatusName => "Pending";
    public override bool IsTerminal => false;
    public override bool IsActive => false;
}

public sealed class ScheduledRunInstance : RunInstance
{
    public override string StatusName => "Scheduled";
    public override bool IsTerminal => false;
    public override bool IsActive => true;
}

public sealed class StartingRunInstance : RunInstance
{
    [MaxLength(128)] public string? ContainerId { get; set; }
    public override string StatusName => "Starting";
    public override bool IsTerminal => false;
    public override bool IsActive => true;
}

public sealed class RunningRunInstance : RunInstance
{
    [MaxLength(128)] public string ContainerId { get; set; } = default!;
    public DateTimeOffset StartedAt { get; set; }
    public override string StatusName => "Running";
    public override bool IsTerminal => false;
    public override bool IsActive => true;
}

public sealed class StoppingRunInstance : RunInstance
{
    [MaxLength(128)] public string ContainerId { get; set; } = default!;
    public DateTimeOffset StartedAt { get; set; }
    public override string StatusName => "Stopping";
    public override bool IsTerminal => false;
    public override bool IsActive => true;
}

public sealed class StoppedRunInstance : RunInstance
{
    [MaxLength(128)] public string? ContainerId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset StoppedAt { get; set; }
    public override string StatusName => "Stopped";
    public override bool IsTerminal => true;
    public override bool IsActive => false;
}

public sealed class FailedRunInstance : RunInstance
{
    [MaxLength(128)] public string? ContainerId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? StoppedAt { get; set; }
    public string FailureReason { get; set; } = default!;
    public override string StatusName => "Failed";
    public override bool IsTerminal => true;
    public override bool IsActive => false;
}

// Backtest instances

public abstract class BacktestInstance : Instance
{
    public sealed override string KindName => "Backtest";
    public string? BacktestSettingsJson { get; set; }
}

public sealed class PendingBacktestInstance : BacktestInstance
{
    public override string StatusName => "Pending";
    public override bool IsTerminal => false;
    public override bool IsActive => false;
}

public sealed class ScheduledBacktestInstance : BacktestInstance
{
    public override string StatusName => "Scheduled";
    public override bool IsTerminal => false;
    public override bool IsActive => true;
}

public sealed class StartingBacktestInstance : BacktestInstance
{
    [MaxLength(128)] public string? ContainerId { get; set; }
    public override string StatusName => "Starting";
    public override bool IsTerminal => false;
    public override bool IsActive => true;
}

public sealed class RunningBacktestInstance : BacktestInstance
{
    [MaxLength(128)] public string ContainerId { get; set; } = default!;
    public DateTimeOffset StartedAt { get; set; }
    public override string StatusName => "Running";
    public override bool IsTerminal => false;
    public override bool IsActive => true;
}

public sealed class StoppingBacktestInstance : BacktestInstance
{
    [MaxLength(128)] public string ContainerId { get; set; } = default!;
    public DateTimeOffset StartedAt { get; set; }
    public override string StatusName => "Stopping";
    public override bool IsTerminal => false;
    public override bool IsActive => true;
}

public sealed class CompletedBacktestInstance : BacktestInstance
{
    [MaxLength(128)] public string? ContainerId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset StoppedAt { get; set; }
    public string? ResultJsonPath { get; set; }
    public override string StatusName => "Completed";
    public override bool IsTerminal => true;
    public override bool IsActive => false;
}

public sealed class FailedBacktestInstance : BacktestInstance
{
    [MaxLength(128)] public string? ContainerId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? StoppedAt { get; set; }
    public string FailureReason { get; set; } = default!;
    public override string StatusName => "Failed";
    public override bool IsTerminal => true;
    public override bool IsActive => false;
}

// ---------------- InstanceLog / ViewerGrant ----------------

public class InstanceLog : ISoftDeletable
{
    public long Id { get; set; }
    public InstanceId InstanceId { get; set; }
    public Instance Instance { get; set; } = default!;
    public DateTimeOffset Time { get; set; }
    [MaxLength(8)] public string Stream { get; set; } = "out";
    public string Line { get; set; } = default!;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class ViewerGrant : ISoftDeletable
{
    public UserId ViewerId { get; set; }
    public AppUser Viewer { get; set; } = default!;
    public InstanceId InstanceId { get; set; }
    public Instance Instance { get; set; } = default!;
    public DateTimeOffset GrantedAt { get; set; }
    public UserId GrantedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

// ---------------- MCP / Audit / Settings ----------------

public class McpApiKey : AuditedEntity<McpApiKeyId>
{
    public UserId UserId { get; set; }
    public AppUser User { get; set; } = default!;
    [MaxLength(64)] public string KeyPrefix { get; set; } = default!;
    [MaxLength(128)] public string KeyHash { get; set; } = default!;
    [MaxLength(128)] public string Label { get; set; } = default!;
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public UserId? UserId { get; set; }
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;
    [MaxLength(64)] public string Action { get; set; } = default!;
    [MaxLength(64)] public string EntityType { get; set; } = default!;
    public Guid? EntityId { get; set; }
    [MaxLength(45)] public string? Ip { get; set; }
    public string? DetailsJson { get; set; }
}

public class AppSetting
{
    [MaxLength(64)] public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
