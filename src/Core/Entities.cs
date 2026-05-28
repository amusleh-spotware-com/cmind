using System.ComponentModel.DataAnnotations;

namespace Core;

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}

public abstract class AuditedEntity : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class AppUser : AuditedEntity
{
    [MaxLength(256)] public string Email { get; set; } = default!;
    [MaxLength(256)] public string NormalizedEmail { get; set; } = default!;
    [MaxLength(512)] public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; } = UserRole.User;
    public Guid? CreatedByUserId { get; set; }
    public bool MustChangePassword { get; set; }
    public bool IsLockedOut { get; set; }
    public int AccessFailedCount { get; set; }
    public bool ViewerSeeAllInstances { get; set; }
    [MaxLength(64)] public string? MfaSecret { get; set; }
    public bool MfaEnabled { get; set; }
    public byte[] SecurityStamp { get; set; } = default!;
}

public class CTraderIdAccount : AuditedEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;
    [MaxLength(256)] public string Username { get; set; } = default!;
    public byte[] EncryptedPassword { get; set; } = default!;
    public List<TradingAccount> TradingAccounts { get; set; } = [];
}

public class TradingAccount : AuditedEntity
{
    public Guid CTidId { get; set; }
    public CTraderIdAccount CTid { get; set; } = default!;
    public long AccountNumber { get; set; }
    [MaxLength(128)] public string Broker { get; set; } = default!;
    public bool IsLive { get; set; }
    public byte[]? EncryptedToken { get; set; }
    [MaxLength(128)] public string? Label { get; set; }
}

public class CBot : AuditedEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;
    [MaxLength(256)] public string Name { get; set; } = default!;
    public byte[] EncryptedAlgo { get; set; } = default!;
    public int Version { get; set; } = 1;
    public Guid? SourceProjectId { get; set; }
    public CBotSourceProject? SourceProject { get; set; }
    public List<ParamSet> ParamSets { get; set; } = [];
}

public class CBotSourceProject : AuditedEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;
    public CBotLanguage Language { get; set; } = CBotLanguage.CSharp;
    [MaxLength(256)] public string Name { get; set; } = default!;
    public string ProjectFilesJson { get; set; } = "{}";
    public string? LastBuildLog { get; set; }
    public DateTimeOffset? LastBuildAt { get; set; }
    public bool LastBuildSucceeded { get; set; }
}

public class ParamSet : AuditedEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;
    public Guid CBotId { get; set; }
    public CBot CBot { get; set; } = default!;
    [MaxLength(256)] public string Name { get; set; } = default!;
    public string JsonContent { get; set; } = "{}";
}

public class Node : AuditedEntity
{
    [MaxLength(128)] public string Name { get; set; } = default!;
    [MaxLength(256)] public string Host { get; set; } = default!;
    public int SshPort { get; set; } = 22;
    [MaxLength(64)] public string SshUser { get; set; } = default!;
    public byte[] EncryptedSshKey { get; set; } = default!;
    public byte[]? EncryptedSshKeyPassphrase { get; set; }
    public NodeMode Mode { get; set; } = NodeMode.Mixed;
    public NodeStatus Status { get; set; } = NodeStatus.Active;
    [MaxLength(256)] public string DataDirPath { get; set; } = "/var/ctw/data";
    public int MaxInstances { get; set; } = 10;
    public NodeStats? LatestStats { get; set; }
}

public class NodeStats
{
    public Guid NodeId { get; set; }
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

public class Instance : AuditedEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;
    public Guid CBotId { get; set; }
    public CBot CBot { get; set; } = default!;
    public Guid? TradingAccountId { get; set; }
    public TradingAccount? TradingAccount { get; set; }
    public Guid? NodeId { get; set; }
    public Node? Node { get; set; }
    public InstanceType Type { get; set; } = InstanceType.Run;
    public InstanceStatus Status { get; set; } = InstanceStatus.Pending;
    [MaxLength(128)] public string DockerImageTag { get; set; } = "latest";
    [MaxLength(128)] public string? ContainerId { get; set; }
    [MaxLength(32)] public string? Symbol { get; set; }
    [MaxLength(16)] public string? Timeframe { get; set; }
    public Guid? ParamSetId { get; set; }
    public ParamSet? ParamSet { get; set; }
    public string? BacktestSettingsJson { get; set; }
    public string? ResultJsonPath { get; set; }
    [MaxLength(512)] public string? DataDirSubPath { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? StoppedAt { get; set; }
    public string? FailureReason { get; set; }
}

public class InstanceLog
{
    public long Id { get; set; }
    public Guid InstanceId { get; set; }
    public Instance Instance { get; set; } = default!;
    public DateTimeOffset Time { get; set; }
    [MaxLength(8)] public string Stream { get; set; } = "out";
    public string Line { get; set; } = default!;
}

public class ViewerGrant
{
    public Guid ViewerId { get; set; }
    public AppUser Viewer { get; set; } = default!;
    public Guid InstanceId { get; set; }
    public Instance Instance { get; set; } = default!;
    public DateTimeOffset GrantedAt { get; set; }
    public Guid GrantedByUserId { get; set; }
}

public class McpApiKey : AuditedEntity
{
    public Guid UserId { get; set; }
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
    public Guid? UserId { get; set; }
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
