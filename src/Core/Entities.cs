using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Agent;
using Core.Constants;
using Core.Domain;

namespace Core;

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}

public abstract class AuditedEntity<TId> : ISoftDeletable, IHasDomainEvents, IAggregateRoot
    where TId : struct, IStronglyTypedId<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public TId Id { get; private set; } = TId.New();
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    [NotMapped] public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    protected internal void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
    protected internal void PreserveCreatedAt(DateTimeOffset createdAt) => CreatedAt = createdAt;
}

// ---------------- Access: AppUser hierarchy by role ----------------

public abstract class AppUser : AuditedEntity<UserId>
{
    [MaxLength(256)] public string Email { get; internal set; } = default!;
    [MaxLength(256)] public string NormalizedEmail { get; internal set; } = default!;
    [MaxLength(512)] public string PasswordHash { get; private set; } = default!;
    public UserId? CreatedByUserId { get; internal set; }
    public bool MustChangePassword { get; private set; }
    public bool IsLockedOut { get; private set; }
    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEnd { get; private set; }
    [MaxLength(64)] public string? MfaSecret { get; private set; }
    public bool MfaEnabled { get; private set; }
    public byte[] SecurityStamp { get; private set; } = default!;

    public abstract string RoleName { get; }
    public abstract int RoleRank { get; }

    protected static TUser Init<TUser>(TUser user, Email email, string passwordHash, byte[] securityStamp,
        bool mustChangePassword, UserId? createdBy)
        where TUser : AppUser
    {
        user.Email = email.Value;
        user.NormalizedEmail = email.Normalized;
        user.PasswordHash = DomainGuard.AgainstNullOrWhiteSpace(passwordHash, DomainErrors.NameRequired);
        user.SecurityStamp = securityStamp;
        user.MustChangePassword = mustChangePassword;
        user.CreatedByUserId = createdBy;
        return user;
    }

    public bool RecordFailedLogin(int maxAttempts, TimeSpan lockoutDuration, DateTimeOffset now)
    {
        AccessFailedCount++;
        if (AccessFailedCount < maxAttempts)
        {
            return false;
        }

        LockoutEnd = now.Add(lockoutDuration);
        AccessFailedCount = 0;

        return true;
    }

    public void RecordSuccessfulLogin()
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
    }

    public bool IsCurrentlyLockedOut(DateTimeOffset now) =>
        IsLockedOut || (LockoutEnd is { } end && end > now);

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = DomainGuard.AgainstNullOrWhiteSpace(passwordHash, DomainErrors.NameRequired);
        MustChangePassword = false;
    }

    public void ResetPassword(string passwordHash, byte[] securityStamp)
    {
        PasswordHash = DomainGuard.AgainstNullOrWhiteSpace(passwordHash, DomainErrors.NameRequired);
        SecurityStamp = securityStamp;
        MustChangePassword = true;
        IsLockedOut = false;
        AccessFailedCount = 0;
        LockoutEnd = null;
    }

    public void EnableMfa(string secret)
    {
        MfaSecret = DomainGuard.AgainstNullOrWhiteSpace(secret, DomainErrors.NameRequired);
        MfaEnabled = true;
    }

    public void DisableMfa()
    {
        MfaSecret = null;
        MfaEnabled = false;
    }

    // GDPR erasure: scrub personally identifying data while keeping the (soft-deleted) row so that
    // referential history and audit records remain coherent.
    public void Anonymize()
    {
        Email = $"erased-{Id.Value:N}@erased.invalid";
        NormalizedEmail = Email.ToUpperInvariant();
        MfaSecret = null;
        MfaEnabled = false;
    }
}

public sealed class OwnerUser : AppUser
{
    public override string RoleName => "Owner";
    public override int RoleRank => 0;

    public static OwnerUser Create(Email email, string passwordHash, byte[] securityStamp,
        bool mustChangePassword = true, UserId? createdBy = null)
        => Init(new OwnerUser(), email, passwordHash, securityStamp, mustChangePassword, createdBy);
}

public sealed class AdminUser : AppUser
{
    public override string RoleName => "Admin";
    public override int RoleRank => 1;

    public static AdminUser Create(Email email, string passwordHash, byte[] securityStamp,
        bool mustChangePassword = true, UserId? createdBy = null)
        => Init(new AdminUser(), email, passwordHash, securityStamp, mustChangePassword, createdBy);
}

public sealed class RegularUser : AppUser
{
    public override string RoleName => "User";
    public override int RoleRank => 2;

    public static RegularUser Create(Email email, string passwordHash, byte[] securityStamp,
        bool mustChangePassword = true, UserId? createdBy = null)
        => Init(new RegularUser(), email, passwordHash, securityStamp, mustChangePassword, createdBy);
}

public sealed class ViewerUser : AppUser
{
    public override string RoleName => "Viewer";
    public override int RoleRank => 3;
    public bool SeeAllInstances { get; private set; }

    public static ViewerUser Create(Email email, string passwordHash, byte[] securityStamp,
        bool seeAllInstances, bool mustChangePassword = true, UserId? createdBy = null)
    {
        var user = Init(new ViewerUser(), email, passwordHash, securityStamp, mustChangePassword, createdBy);
        user.SeeAllInstances = seeAllInstances;
        return user;
    }
}

// ---------------- Access: cTrader ID ----------------

public class CTraderIdAccount : AuditedEntity<CtidId>
{
    private readonly List<TradingAccount> _tradingAccounts = [];

    public UserId UserId { get; private set; }
    public AppUser User { get; private set; } = default!;
    [MaxLength(256)] public string Username { get; private set; } = default!;
    public byte[] EncryptedPassword { get; private set; } = default!;
    public long? CtidUserId { get; private set; }
    public IReadOnlyList<TradingAccount> TradingAccounts => _tradingAccounts;

    public static CTraderIdAccount Create(UserId userId, string username, byte[] encryptedPassword)
        => new()
        {
            UserId = userId,
            Username = DomainGuard.AgainstNullOrWhiteSpace(username, DomainErrors.NameRequired),
            EncryptedPassword = encryptedPassword
        };

    public static CTraderIdAccount CreateForOpenApi(UserId userId, CtidUserId ctidUserId, string username)
        => new()
        {
            UserId = userId,
            Username = DomainGuard.AgainstNullOrWhiteSpace(username, DomainErrors.NameRequired),
            EncryptedPassword = [],
            CtidUserId = ctidUserId.Value
        };

    public void UpdateUsername(string username)
    {
        Username = DomainGuard.AgainstNullOrWhiteSpace(username, DomainErrors.NameRequired);
    }

    public void UpdatePassword(byte[] encryptedPassword)
    {
        EncryptedPassword = encryptedPassword;
    }

    public TradingAccount AddTradingAccount(long accountNumber, string broker, bool isLive, string? label)
    {
        var account = TradingAccount.Create(Id, accountNumber, broker, isLive, label);
        _tradingAccounts.Add(account);

        return account;
    }

    public TradingAccount LinkOpenApiAccount(long accountNumber, string broker, bool isLive,
        CtidTraderAccountId ctidTraderAccountId, OpenApiAuthorizationId authorizationId, string? label)
    {
        var existing = _tradingAccounts.FirstOrDefault(a => a.AccountNumber == accountNumber);
        if (existing is not null)
        {
            existing.LinkOpenApi(ctidTraderAccountId, authorizationId);

            return existing;
        }

        var account = TradingAccount.CreateFromOpenApi(
            Id, accountNumber, broker, isLive, ctidTraderAccountId, authorizationId, label);
        _tradingAccounts.Add(account);

        return account;
    }
}

public class TradingAccount : AuditedEntity<TradingAccountId>
{
    public CtidId CTidId { get; private set; }
    public CTraderIdAccount CTid { get; private set; } = default!;
    public long AccountNumber { get; private set; }
    [MaxLength(128)] public string Broker { get; private set; } = default!;
    public bool IsLive { get; private set; }
    public byte[]? EncryptedToken { get; private set; }
    [MaxLength(128)] public string? Label { get; private set; }
    public AccountLinkMethod LinkMethod { get; private set; } = AccountLinkMethod.Cid;
    public long? CtidTraderAccountId { get; private set; }
    public OpenApiAuthorizationId? OpenApiAuthorizationId { get; private set; }

    public static TradingAccount Create(CtidId ctidId, long accountNumber, string broker, bool isLive, string? label)
        => new()
        {
            CTidId = ctidId,
            AccountNumber = accountNumber,
            Broker = DomainGuard.AgainstNullOrWhiteSpace(broker, DomainErrors.NameRequired),
            IsLive = isLive,
            Label = label,
            LinkMethod = AccountLinkMethod.Cid
        };

    public static TradingAccount CreateFromOpenApi(CtidId ctidId, long accountNumber, string broker, bool isLive,
        CtidTraderAccountId ctidTraderAccountId, OpenApiAuthorizationId authorizationId, string? label)
        => new()
        {
            CTidId = ctidId,
            AccountNumber = accountNumber,
            Broker = DomainGuard.AgainstNullOrWhiteSpace(broker, DomainErrors.NameRequired),
            IsLive = isLive,
            Label = label,
            LinkMethod = AccountLinkMethod.OpenApi,
            CtidTraderAccountId = ctidTraderAccountId.Value,
            OpenApiAuthorizationId = authorizationId
        };

    public void SetToken(byte[]? encryptedToken)
    {
        EncryptedToken = encryptedToken;
    }

    public void LinkOpenApi(CtidTraderAccountId ctidTraderAccountId, OpenApiAuthorizationId authorizationId)
    {
        CtidTraderAccountId = ctidTraderAccountId.Value;
        OpenApiAuthorizationId = authorizationId;
        LinkMethod |= AccountLinkMethod.OpenApi;
    }
}

// ---------------- Authoring: CBot ----------------

public class CBot : AuditedEntity<CBotId>
{
    private readonly List<ParamSet> _paramSets = [];

    public UserId UserId { get; private set; }
    public AppUser User { get; private set; } = default!;
    [MaxLength(256)] public string Name { get; private set; } = default!;
    public byte[] EncryptedAlgo { get; private set; } = default!;
    public int Version { get; private set; } = 1;
    public CBotSourceProjectId? SourceProjectId { get; private set; }
    public CBotSourceProject? SourceProject { get; private set; }
    public IReadOnlyList<ParamSet> ParamSets => _paramSets;

    public static CBot Create(UserId userId, string name, byte[] encryptedAlgo,
        CBotSourceProjectId? sourceProjectId = null)
        => new()
        {
            UserId = userId,
            Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired),
            EncryptedAlgo = encryptedAlgo,
            SourceProjectId = sourceProjectId,
            Version = 1
        };

    public void Rename(string name)
    {
        Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired);
    }

    public void UpdateAlgo(byte[] encryptedAlgo, CBotSourceProjectId? sourceProjectId = null)
    {
        EncryptedAlgo = encryptedAlgo;
        if (sourceProjectId is { } sid) SourceProjectId = sid;
        Version++;
    }

    public ParamSet AddParamSet(string name, string jsonContent)
    {
        var paramSet = ParamSet.Create(UserId, Id, name, jsonContent);
        _paramSets.Add(paramSet);

        return paramSet;
    }
}

// ---------------- Authoring: CBotSourceProject hierarchy by language ----------------

public abstract class CBotSourceProject : AuditedEntity<CBotSourceProjectId>
{
    public UserId UserId { get; private set; }
    public AppUser User { get; private set; } = default!;
    [MaxLength(256)] public string Name { get; private set; } = default!;
    public byte[] EncryptedProjectFiles { get; private set; } = [];
    public string? LastBuildLog { get; private set; }
    public DateTimeOffset? LastBuildAt { get; private set; }
    public bool LastBuildSucceeded { get; private set; }

    public abstract string LanguageName { get; }
    public abstract string FileExtension { get; }

    protected static TProject Init<TProject>(TProject project, UserId userId, string name)
        where TProject : CBotSourceProject
    {
        project.UserId = userId;
        project.Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired);
        return project;
    }

    public void SetFiles(byte[] encryptedProjectFiles)
    {
        EncryptedProjectFiles = encryptedProjectFiles;
    }

    public void RecordBuild(string log, bool succeeded, DateTimeOffset now)
    {
        LastBuildLog = log;
        LastBuildAt = now;
        LastBuildSucceeded = succeeded;
    }
}

public sealed class CSharpProject : CBotSourceProject
{
    public override string LanguageName => "CSharp";
    public override string FileExtension => ".cs";

    public static CSharpProject Create(UserId userId, string name) => Init(new CSharpProject(), userId, name);
}

public sealed class PythonProject : CBotSourceProject
{
    public override string LanguageName => "Python";
    public override string FileExtension => ".py";

    public static PythonProject Create(UserId userId, string name) => Init(new PythonProject(), userId, name);
}

// ---------------- Authoring: ParamSet ----------------

public class ParamSet : AuditedEntity<ParamSetId>
{
    public UserId UserId { get; private set; }
    public AppUser User { get; private set; } = default!;
    public CBotId CBotId { get; private set; }
    public CBot CBot { get; private set; } = default!;
    [MaxLength(256)] public string Name { get; private set; } = default!;
    public string JsonContent { get; private set; } = "{}";

    public static ParamSet Create(UserId userId, CBotId cbotId, string name, string jsonContent)
        => new()
        {
            UserId = userId,
            CBotId = cbotId,
            Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired),
            JsonContent = string.IsNullOrWhiteSpace(jsonContent) ? "{}" : jsonContent
        };

    public void Update(string name, string jsonContent)
    {
        Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired);
        JsonContent = string.IsNullOrWhiteSpace(jsonContent) ? "{}" : jsonContent;
    }
}

// ---------------- Execution: Node hierarchy by Mode+Status ----------------

public abstract class Node : AuditedEntity<NodeId>
{
    [MaxLength(128)] public string Name { get; internal set; } = default!;
    [MaxLength(256)] public string DataDirPath { get; internal set; } = "/var/app/data";
    public int MaxInstances { get; internal set; } = 10;
    public NodeStats? LatestStats { get; private set; }

    public abstract string ModeName { get; }
    public abstract string StatusName { get; }
    public abstract bool IsActive { get; }
    public abstract bool AcceptsRun { get; }
    public abstract bool AcceptsBacktest { get; }
    public abstract bool IsLocal { get; }

    protected static void InitCore(Node node, string name, string dataDirPath, int maxInstances)
    {
        if (maxInstances <= 0) throw new DomainException(DomainErrors.NodeMaxInstancesInvalid);
        node.Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired);
        node.DataDirPath = dataDirPath;
        node.MaxInstances = maxInstances;
    }
}

public abstract class RemoteNode : Node
{
    [MaxLength(512)] public string BaseUrl { get; internal set; } = default!;
    public byte[] EncryptedApiSecret { get; internal set; } = default!;
    public DateTimeOffset? LastHeartbeatAt { get; private set; }
    public bool IsReachable { get; private set; } = true;

    public override bool IsLocal => false;

    public static RemoteNode Create(NodeMode mode, string name, string baseUrl, byte[] encryptedApiSecret,
        string dataDirPath, int maxInstances)
    {
        RemoteNode node = mode.Name switch
        {
            nameof(NodeMode.Run) => new ActiveRunNode(),
            nameof(NodeMode.Backtest) => new ActiveBacktestNode(),
            nameof(NodeMode.Mixed) => new ActiveMixedNode(),
            _ => throw new DomainException(DomainErrors.NameRequired, $"Invalid node mode: {mode.Name}")
        };
        InitCore(node, name, dataDirPath, maxInstances);
        node.BaseUrl = DomainGuard.AgainstNullOrWhiteSpace(baseUrl, DomainErrors.NameRequired).TrimEnd('/');
        node.EncryptedApiSecret = encryptedApiSecret;
        return node;
    }

    public static RemoteNode SelfRegister(NodeMode mode, string name, NodeEndpointUrl endpoint,
        byte[] encryptedApiSecret, string dataDirPath, int maxInstances, DateTimeOffset now)
    {
        var node = Create(mode, name, endpoint.Value, encryptedApiSecret, dataDirPath, maxInstances);
        node.LastHeartbeatAt = now;
        node.IsReachable = true;
        node.RaiseDomainEvent(new NodeRegistered(node.Id, node.Name));
        return node;
    }

    public void RecordHeartbeat(NodeEndpointUrl endpoint, int maxInstances, DateTimeOffset now)
    {
        if (maxInstances <= 0) throw new DomainException(DomainErrors.NodeMaxInstancesInvalid);
        BaseUrl = endpoint.Value;
        MaxInstances = maxInstances;
        LastHeartbeatAt = now;
        var wasUnreachable = !IsReachable;
        IsReachable = true;

        if (wasUnreachable) RaiseDomainEvent(new NodeCameOnline(Id, Name));
    }

    public bool IsHeartbeatStale(TimeSpan ttl, DateTimeOffset asOf) =>
        LastHeartbeatAt is { } last && asOf - last > ttl;

    public void MarkUnreachable()
    {
        if (!IsReachable) return;
        IsReachable = false;

        RaiseDomainEvent(new NodeWentOffline(Id, Name));
    }
}

public sealed class ActiveRunNode : RemoteNode
{
    public override string ModeName => "Run";
    public override string StatusName => IsReachable ? "Active" : "Unreachable";
    public override bool IsActive => IsReachable;
    public override bool AcceptsRun => IsReachable;
    public override bool AcceptsBacktest => false;
}

public sealed class ActiveBacktestNode : RemoteNode
{
    public override string ModeName => "Backtest";
    public override string StatusName => IsReachable ? "Active" : "Unreachable";
    public override bool IsActive => IsReachable;
    public override bool AcceptsRun => false;
    public override bool AcceptsBacktest => IsReachable;
}

public sealed class ActiveMixedNode : RemoteNode
{
    public override string ModeName => "Mixed";
    public override string StatusName => IsReachable ? "Active" : "Unreachable";
    public override bool IsActive => IsReachable;
    public override bool AcceptsRun => IsReachable;
    public override bool AcceptsBacktest => IsReachable;
}

public sealed class DecommissioningNode : RemoteNode
{
    public override string ModeName => "Decommissioning";
    public override string StatusName => "Decommissioning";
    public override bool IsActive => false;
    public override bool AcceptsRun => false;
    public override bool AcceptsBacktest => false;
}

public sealed class OfflineNode : RemoteNode
{
    public override string ModeName => "Offline";
    public override string StatusName => "Offline";
    public override bool IsActive => false;
    public override bool AcceptsRun => false;
    public override bool AcceptsBacktest => false;
}

public sealed class LocalNode : Node
{
    public bool Enabled { get; internal set; }
    public override string ModeName => "Mixed";
    public override string StatusName => Enabled ? "Active" : "Disabled";
    public override bool IsActive => Enabled;
    public override bool AcceptsRun => Enabled;
    public override bool AcceptsBacktest => Enabled;
    public override bool IsLocal => true;

    public static LocalNode Create(string name, string dataDirPath, int maxInstances, bool enabled)
    {
        var node = new LocalNode();
        InitCore(node, name, dataDirPath, maxInstances);
        node.Enabled = enabled;
        return node;
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
    }
}

public class NodeStats
{
    public NodeId NodeId { get; private set; }
    public Node Node { get; private set; } = default!;
    public double CpuPercent { get; private set; }
    public long MemUsedBytes { get; private set; }
    public long MemTotalBytes { get; private set; }
    public long DiskUsedBytes { get; private set; }
    public long DiskTotalBytes { get; private set; }
    public long BacktestDataUsedBytes { get; private set; }
    public int RunningCount { get; private set; }
    public int BacktestCount { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static NodeStats Create(NodeId nodeId, double cpuPercent, long memUsedBytes, long memTotalBytes,
        long diskUsedBytes, long diskTotalBytes, long backtestDataUsedBytes, DateTimeOffset now,
        int runningCount = 0, int backtestCount = 0)
        => new()
        {
            NodeId = nodeId,
            CpuPercent = cpuPercent,
            MemUsedBytes = memUsedBytes,
            MemTotalBytes = memTotalBytes,
            DiskUsedBytes = diskUsedBytes,
            DiskTotalBytes = diskTotalBytes,
            BacktestDataUsedBytes = backtestDataUsedBytes,
            RunningCount = runningCount,
            BacktestCount = backtestCount,
            UpdatedAt = now
        };

    public void SetInstanceCounts(int runningCount, int backtestCount, DateTimeOffset now)
    {
        RunningCount = runningCount;
        BacktestCount = backtestCount;
        UpdatedAt = now;
    }
}

// ---------------- Execution: Instance hierarchy by Kind + Status ----------------

public abstract class Instance : AuditedEntity<InstanceId>
{
    public UserId UserId { get; internal set; }
    public AppUser User { get; private set; } = default!;
    public CBotId CBotId { get; internal set; }
    public CBot CBot { get; private set; } = default!;
    public TradingAccountId? TradingAccountId { get; internal set; }
    public TradingAccount? TradingAccount { get; private set; }
    public NodeId? NodeId { get; internal set; }
    public Node? Node { get; private set; }
    [MaxLength(128)] public string DockerImageTag { get; internal set; } = "latest";
    [MaxLength(32)] public string? Symbol { get; internal set; }
    [MaxLength(16)] public string? Timeframe { get; internal set; }
    public ParamSetId? ParamSetId { get; internal set; }
    public ParamSet? ParamSet { get; private set; }
    [MaxLength(512)] public string? DataDirSubPath { get; internal set; }

    public abstract string KindName { get; }
    public abstract string StatusName { get; }
    public abstract bool IsTerminal { get; }
    public abstract bool IsActive { get; }

    public void AttachNode(Node node)
    {
        Node = node;
        NodeId = node.Id;
    }

    public void SetDataDirSubPath(string dataDirSubPath) => DataDirSubPath = dataDirSubPath;

    protected static void CopyExecutionState(Instance source, Instance target)
    {
        target.PreserveCreatedAt(source.CreatedAt);
        target.UserId = source.UserId;
        target.CBotId = source.CBotId;
        target.TradingAccountId = source.TradingAccountId;
        target.NodeId = source.NodeId;
        target.DockerImageTag = source.DockerImageTag;
        target.Symbol = source.Symbol;
        target.Timeframe = source.Timeframe;
        target.ParamSetId = source.ParamSetId;
        target.DataDirSubPath = source.DataDirSubPath;
    }
}

// Run instances

public abstract class RunInstance : Instance
{
    public sealed override string KindName => "Run";

    public FailedRunInstance ToFailed(string reason, DateTimeOffset now) => FailedRunInstance.From(this, reason, now);

    public static StartingRunInstance CreateStarting(UserId userId, CBotId cbotId, NodeId nodeId,
        DockerImageTag imageTag, Symbol symbol, Timeframe timeframe,
        TradingAccountId? tradingAccountId = null, ParamSetId? paramSetId = null)
        => new()
        {
            UserId = userId,
            CBotId = cbotId,
            NodeId = nodeId,
            DockerImageTag = imageTag.Value,
            Symbol = symbol.Value,
            Timeframe = timeframe.Value,
            TradingAccountId = tradingAccountId,
            ParamSetId = paramSetId
        };

    public static FailedRunInstance CreateFailed(UserId userId, CBotId cbotId, NodeId nodeId,
        DockerImageTag imageTag, Symbol symbol, Timeframe timeframe, string failureReason,
        TradingAccountId? tradingAccountId = null, ParamSetId? paramSetId = null)
    {
        var failed = new FailedRunInstance
        {
            UserId = userId,
            CBotId = cbotId,
            NodeId = nodeId,
            DockerImageTag = imageTag.Value,
            Symbol = symbol.Value,
            Timeframe = timeframe.Value,
            TradingAccountId = tradingAccountId,
            ParamSetId = paramSetId,
            FailureReason = failureReason
        };
        failed.RaiseDomainEvent(new InstanceFailed(failed.Id, failed.UserId, failureReason));
        return failed;
    }
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
    [MaxLength(128)] public string? ContainerId { get; internal set; }
    public override string StatusName => "Starting";
    public override bool IsTerminal => false;
    public override bool IsActive => true;

    public RunningRunInstance ToRunning(string containerId, DateTimeOffset now)
    {
        var running = new RunningRunInstance {ContainerId = containerId, StartedAt = now};
        CopyExecutionState(this, running);
        running.RaiseDomainEvent(new InstanceStarted(running.Id, running.UserId, containerId));
        return running;
    }

    public StoppedRunInstance ToStopped(DateTimeOffset stoppedAt)
    {
        var stopped = new StoppedRunInstance {ContainerId = ContainerId, StoppedAt = stoppedAt};
        CopyExecutionState(this, stopped);
        return stopped;
    }
}

public sealed class RunningRunInstance : RunInstance
{
    [MaxLength(128)] public string ContainerId { get; internal set; } = default!;
    public DateTimeOffset StartedAt { get; internal set; }
    public override string StatusName => "Running";
    public override bool IsTerminal => false;
    public override bool IsActive => true;

    public StoppedRunInstance ToStopped(DateTimeOffset stoppedAt)
    {
        var stopped = new StoppedRunInstance
        {
            ContainerId = ContainerId,
            StartedAt = StartedAt,
            StoppedAt = stoppedAt
        };
        CopyExecutionState(this, stopped);
        stopped.RaiseDomainEvent(new InstanceStopped(stopped.Id, stopped.UserId));
        return stopped;
    }
}

public sealed class StoppingRunInstance : RunInstance
{
    [MaxLength(128)] public string ContainerId { get; internal set; } = default!;
    public DateTimeOffset StartedAt { get; internal set; }
    public override string StatusName => "Stopping";
    public override bool IsTerminal => false;
    public override bool IsActive => true;
}

public sealed class StoppedRunInstance : RunInstance
{
    [MaxLength(128)] public string? ContainerId { get; internal set; }
    public DateTimeOffset? StartedAt { get; internal set; }
    public DateTimeOffset StoppedAt { get; internal set; }
    public override string StatusName => "Stopped";
    public override bool IsTerminal => true;
    public override bool IsActive => false;
}

public sealed class FailedRunInstance : RunInstance
{
    [MaxLength(128)] public string? ContainerId { get; internal set; }
    public DateTimeOffset? StartedAt { get; internal set; }
    public DateTimeOffset? StoppedAt { get; internal set; }
    public string FailureReason { get; internal set; } = default!;
    public override string StatusName => "Failed";
    public override bool IsTerminal => true;
    public override bool IsActive => false;

    internal static FailedRunInstance From(RunInstance source, string reason, DateTimeOffset now)
    {
        var failed = new FailedRunInstance
        {
            FailureReason = reason,
            ContainerId = (source as StartingRunInstance)?.ContainerId
                          ?? (source as RunningRunInstance)?.ContainerId,
            StartedAt = (source as RunningRunInstance)?.StartedAt,
            StoppedAt = now
        };
        CopyExecutionState(source, failed);
        failed.RaiseDomainEvent(new InstanceFailed(failed.Id, failed.UserId, reason));
        return failed;
    }
}

// Backtest instances

public abstract class BacktestInstance : Instance
{
    public sealed override string KindName => "Backtest";
    public string? BacktestSettingsJson { get; internal set; }

    public FailedBacktestInstance ToFailed(string reason, DateTimeOffset now) => FailedBacktestInstance.From(this, reason, now);

    public static StartingBacktestInstance CreateStarting(UserId userId, CBotId cbotId, NodeId nodeId,
        DockerImageTag imageTag, Symbol symbol, Timeframe timeframe, string? backtestSettingsJson,
        TradingAccountId? tradingAccountId = null, ParamSetId? paramSetId = null)
        => new()
        {
            UserId = userId,
            CBotId = cbotId,
            NodeId = nodeId,
            DockerImageTag = imageTag.Value,
            Symbol = symbol.Value,
            Timeframe = timeframe.Value,
            TradingAccountId = tradingAccountId,
            ParamSetId = paramSetId,
            BacktestSettingsJson = backtestSettingsJson
        };

    public static FailedBacktestInstance CreateFailed(UserId userId, CBotId cbotId, NodeId nodeId,
        DockerImageTag imageTag, Symbol symbol, Timeframe timeframe, string failureReason,
        string? backtestSettingsJson = null, TradingAccountId? tradingAccountId = null, ParamSetId? paramSetId = null)
    {
        var failed = new FailedBacktestInstance
        {
            UserId = userId,
            CBotId = cbotId,
            NodeId = nodeId,
            DockerImageTag = imageTag.Value,
            Symbol = symbol.Value,
            Timeframe = timeframe.Value,
            TradingAccountId = tradingAccountId,
            ParamSetId = paramSetId,
            BacktestSettingsJson = backtestSettingsJson,
            FailureReason = failureReason
        };
        failed.RaiseDomainEvent(new InstanceFailed(failed.Id, failed.UserId, failureReason));
        return failed;
    }

    protected static void CopyBacktestState(BacktestInstance source, BacktestInstance target)
    {
        CopyExecutionState(source, target);
        target.BacktestSettingsJson = source.BacktestSettingsJson;
    }
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
    [MaxLength(128)] public string? ContainerId { get; internal set; }
    public override string StatusName => "Starting";
    public override bool IsTerminal => false;
    public override bool IsActive => true;

    public RunningBacktestInstance ToRunning(string containerId, DateTimeOffset now)
    {
        var running = new RunningBacktestInstance {ContainerId = containerId, StartedAt = now};
        CopyBacktestState(this, running);
        running.RaiseDomainEvent(new InstanceStarted(running.Id, running.UserId, containerId));
        return running;
    }
}

public sealed class RunningBacktestInstance : BacktestInstance
{
    [MaxLength(128)] public string ContainerId { get; internal set; } = default!;
    public DateTimeOffset StartedAt { get; internal set; }
    public override string StatusName => "Running";
    public override bool IsTerminal => false;
    public override bool IsActive => true;

    public CompletedBacktestInstance ToCompleted(DateTimeOffset stoppedAt, string? reportJson = null,
        string? resultJsonPath = null)
    {
        var completed = new CompletedBacktestInstance
        {
            ContainerId = ContainerId,
            StartedAt = StartedAt,
            StoppedAt = stoppedAt,
            ReportJson = reportJson,
            ResultJsonPath = resultJsonPath
        };
        CopyBacktestState(this, completed);
        completed.RaiseDomainEvent(new BacktestCompleted(completed.Id, completed.UserId));
        return completed;
    }
}

public sealed class StoppingBacktestInstance : BacktestInstance
{
    [MaxLength(128)] public string ContainerId { get; internal set; } = default!;
    public DateTimeOffset StartedAt { get; internal set; }
    public override string StatusName => "Stopping";
    public override bool IsTerminal => false;
    public override bool IsActive => true;
}

public sealed class CompletedBacktestInstance : BacktestInstance
{
    [MaxLength(128)] public string? ContainerId { get; internal set; }
    public DateTimeOffset? StartedAt { get; internal set; }
    public DateTimeOffset StoppedAt { get; internal set; }
    public string? ResultJsonPath { get; internal set; }
    public string? ReportJson { get; internal set; }
    public override string StatusName => "Completed";
    public override bool IsTerminal => true;
    public override bool IsActive => false;
}

public sealed class FailedBacktestInstance : BacktestInstance
{
    [MaxLength(128)] public string? ContainerId { get; internal set; }
    public DateTimeOffset? StartedAt { get; internal set; }
    public DateTimeOffset? StoppedAt { get; internal set; }
    public string FailureReason { get; internal set; } = default!;
    public override string StatusName => "Failed";
    public override bool IsTerminal => true;
    public override bool IsActive => false;

    internal static FailedBacktestInstance From(BacktestInstance source, string reason, DateTimeOffset now)
    {
        var failed = new FailedBacktestInstance
        {
            FailureReason = reason,
            ContainerId = (source as StartingBacktestInstance)?.ContainerId
                          ?? (source as RunningBacktestInstance)?.ContainerId,
            StartedAt = (source as RunningBacktestInstance)?.StartedAt,
            StoppedAt = now
        };
        CopyBacktestState(source, failed);
        failed.RaiseDomainEvent(new InstanceFailed(failed.Id, failed.UserId, reason));
        return failed;
    }
}

// ---------------- Execution: InstanceLog / ViewerGrant ----------------

public class InstanceLog : ISoftDeletable
{
    public long Id { get; private set; }
    public InstanceId InstanceId { get; private set; }
    public Instance Instance { get; private set; } = default!;
    public DateTimeOffset Time { get; private set; }
    [MaxLength(8)] public string Stream { get; private set; } = "out";
    public string Line { get; private set; } = default!;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static InstanceLog Create(InstanceId instanceId, DateTimeOffset time, string stream, string line)
        => new()
        {
            InstanceId = instanceId,
            Time = time,
            Stream = stream,
            Line = line
        };
}

// Append-only execution-transparency record: one row per copy attempt on one destination, written
// out-of-band by the copy event sink. Not an aggregate — a fact log queried by the transparency read model.
public class CopyExecution : ISoftDeletable
{
    public long Id { get; private set; }
    public Guid ProfileId { get; private set; }
    public long DestinationCtidTraderAccountId { get; private set; }
    public long SourcePositionId { get; private set; }
    [MaxLength(64)] public string Symbol { get; private set; } = default!;
    public Core.CopyTrading.CopyExecutionKind Kind { get; private set; }
    public bool IsBuy { get; private set; }
    public long Volume { get; private set; }
    public double MasterPrice { get; private set; }
    public int? SlippagePoints { get; private set; }
    public double LatencyMilliseconds { get; private set; }
    [MaxLength(128)] public string? Reason { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static CopyExecution From(Core.CopyTrading.CopyExecutionRecord record)
        => new()
        {
            ProfileId = record.ProfileId.Value,
            DestinationCtidTraderAccountId = record.DestinationCtidTraderAccountId,
            SourcePositionId = record.SourcePositionId,
            Symbol = record.Symbol,
            Kind = record.Kind,
            IsBuy = record.IsBuy,
            Volume = record.Volume,
            MasterPrice = record.MasterPrice,
            SlippagePoints = record.SlippagePoints,
            LatencyMilliseconds = record.LatencyMilliseconds,
            Reason = record.Reason,
            OccurredAt = record.OccurredAt
        };
}

// Append-only copy operational notification for the profile owner (destination tripped, account protection,
// prop breach, flatten, token invalidated), written out-of-band by the copy notification sink. Not an
// aggregate — a user-facing feed queried directly, acknowledgeable per row.
public class CopyNotification : ISoftDeletable
{
    public long Id { get; private set; }
    public Guid ProfileId { get; private set; }
    public UserId UserId { get; private set; }
    public long? DestinationCtidTraderAccountId { get; private set; }
    public Core.CopyTrading.CopyNotificationKind Kind { get; private set; }
    public Core.CopyTrading.CopyNotificationSeverity Severity { get; private set; }
    [MaxLength(256)] public string Message { get; private set; } = default!;
    public DateTimeOffset OccurredAt { get; private set; }
    public bool Acknowledged { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static CopyNotification From(Core.CopyTrading.CopyNotificationRecord record, UserId userId)
        => new()
        {
            ProfileId = record.ProfileId.Value,
            UserId = userId,
            DestinationCtidTraderAccountId = record.DestinationCtidTraderAccountId,
            Kind = record.Kind,
            Severity = record.Severity,
            Message = record.Message,
            OccurredAt = record.OccurredAt
        };

    public void Acknowledge() => Acknowledged = true;
}

public class ViewerGrant : ISoftDeletable
{
    public UserId ViewerId { get; private set; }
    public AppUser Viewer { get; private set; } = default!;
    public InstanceId InstanceId { get; private set; }
    public Instance Instance { get; private set; } = default!;
    public DateTimeOffset GrantedAt { get; private set; }
    public UserId GrantedByUserId { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static ViewerGrant Create(UserId viewerId, InstanceId instanceId, UserId grantedByUserId, DateTimeOffset now)
        => new()
        {
            ViewerId = viewerId,
            InstanceId = instanceId,
            GrantedByUserId = grantedByUserId,
            GrantedAt = now
        };
}

// ---------------- Portfolio: Autonomous Portfolio Agent + Decision Journal ----------------

public class AgentMandate : AuditedEntity<AgentMandateId>
{
    private readonly List<AgentProposal> _proposals = [];

    public UserId UserId { get; private set; }
    public AppUser User { get; private set; } = default!;
    public CBotId CBotId { get; private set; }
    public CBot CBot { get; private set; } = default!;
    public TradingAccountId? TradingAccountId { get; private set; }
    public TradingAccount? TradingAccount { get; private set; }
    [MaxLength(128)] public string Name { get; private set; } = default!;
    [MaxLength(1024)] public string Objective { get; private set; } = string.Empty;
    public double RiskPercentPerTrade { get; private set; } = 1.0;
    public double MaxDrawdownPercent { get; private set; } = 20.0;
    [MaxLength(32)] public string Symbol { get; private set; } = AgentConstants.DefaultSymbol;
    [MaxLength(16)] public string Timeframe { get; private set; } = AgentConstants.DefaultTimeframe;
    [MaxLength(128)] public string DockerImageTag { get; private set; } = DockerImages.DefaultTag;
    public string? BacktestSettingsJson { get; private set; }
    public AgentAutonomy Autonomy { get; private set; } = AgentAutonomy.Suggest;
    public bool Enabled { get; private set; }
    public DateTimeOffset? LastRunAt { get; private set; }
    public IReadOnlyList<AgentProposal> Proposals => _proposals;

    public static AgentMandate Create(UserId userId, CBotId cbotId, string name, string objective,
        RiskPercent riskPercentPerTrade, DrawdownPercent maxDrawdownPercent, Symbol symbol, Timeframe timeframe,
        DockerImageTag imageTag, AgentAutonomy autonomy, string? backtestSettingsJson,
        TradingAccountId? tradingAccountId = null)
        => new()
        {
            UserId = userId,
            CBotId = cbotId,
            Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired),
            Objective = objective ?? string.Empty,
            RiskPercentPerTrade = riskPercentPerTrade.Value,
            MaxDrawdownPercent = maxDrawdownPercent.Value,
            Symbol = symbol.Value,
            Timeframe = timeframe.Value,
            DockerImageTag = imageTag.Value,
            Autonomy = autonomy,
            BacktestSettingsJson = backtestSettingsJson,
            TradingAccountId = tradingAccountId
        };

    public void Enable()
    {
        Enabled = true;
    }

    public void Disable()
    {
        Enabled = false;
    }

    public void RecordRun(DateTimeOffset now)
    {
        LastRunAt = now;
    }

    public void Rename(string name)
    {
        Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired);
    }

    public void SetObjective(string objective)
    {
        Objective = objective ?? string.Empty;
    }

    public void SetRiskPerTrade(RiskPercent risk)
    {
        RiskPercentPerTrade = risk.Value;
    }

    public void SetMaxDrawdown(DrawdownPercent drawdown)
    {
        MaxDrawdownPercent = drawdown.Value;
    }

    public void SetSymbol(Symbol symbol)
    {
        Symbol = symbol.Value;
    }

    public void SetTimeframe(Timeframe timeframe)
    {
        Timeframe = timeframe.Value;
    }

    public void SetAutonomy(AgentAutonomy autonomy)
    {
        Autonomy = autonomy;
    }

    public void SetTradingAccount(TradingAccountId tradingAccountId)
    {
        TradingAccountId = tradingAccountId;
    }

    public AgentProposal AddProposal(string kind, string reasoning, string payloadJson, string proposedName)
    {
        var proposal = AgentProposal.Create(Id, UserId, kind, reasoning, payloadJson, proposedName);
        _proposals.Add(proposal);

        RaiseDomainEvent(new AgentProposalCreated(proposal.Id, Id, UserId));
        return proposal;
    }
}

public class AgentProposal : AuditedEntity<AgentProposalId>
{
    public AgentMandateId MandateId { get; private set; }
    public AgentMandate Mandate { get; private set; } = default!;
    public UserId UserId { get; private set; }
    [MaxLength(32)] public string Kind { get; private set; } = AgentConstants.ProposalKindBacktest;
    public string Reasoning { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = "{}";
    [MaxLength(128)] public string ProposedName { get; private set; } = default!;
    public AgentProposalStatus Status { get; private set; } = AgentProposalStatus.Pending;
    public ParamSetId? CreatedParamSetId { get; private set; }
    public InstanceId? CreatedInstanceId { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public UserId? DecidedByUserId { get; private set; }
    public string? FailureReason { get; private set; }

    public static AgentProposal Create(AgentMandateId mandateId, UserId userId, string kind, string reasoning,
        string payloadJson, string proposedName)
        => new()
        {
            MandateId = mandateId,
            UserId = userId,
            Kind = kind,
            Reasoning = reasoning ?? string.Empty,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            ProposedName = DomainGuard.AgainstNullOrWhiteSpace(proposedName, DomainErrors.NameRequired)
        };

    public void Approve(UserId decidedBy, DateTimeOffset now) => Decide(AgentProposalStatus.Approved, decidedBy, now);
    public void Reject(UserId decidedBy, DateTimeOffset now) => Decide(AgentProposalStatus.Rejected, decidedBy, now);

    private void Decide(AgentProposalStatus status, UserId decidedBy, DateTimeOffset now)
    {
        if (Status != AgentProposalStatus.Pending) throw new DomainException(DomainErrors.ProposalNotPending);
        Status = status;
        DecidedByUserId = decidedBy;
        DecidedAt = now;

        RaiseDomainEvent(new AgentProposalDecided(Id, MandateId, status));
    }

    public void MarkExecuted(ParamSetId? createdParamSetId, InstanceId? createdInstanceId)
    {
        Status = AgentProposalStatus.Executed;
        CreatedParamSetId = createdParamSetId;
        CreatedInstanceId = createdInstanceId;

        RaiseDomainEvent(new AgentProposalDecided(Id, MandateId, Status));
    }

    public void MarkFailed(UserId decidedBy, string reason, DateTimeOffset now)
    {
        Status = AgentProposalStatus.Failed;
        FailureReason = reason;
        DecidedByUserId = decidedBy;
        DecidedAt = now;

        RaiseDomainEvent(new AgentProposalDecided(Id, MandateId, Status));
    }
}

// ---------------- Alerts (AI-assessed market alerts) ----------------

public class AlertRule : AuditedEntity<AlertRuleId>
{
    private readonly List<AlertEvent> _events = [];

    public UserId UserId { get; private set; }
    public AppUser User { get; private set; } = default!;
    [MaxLength(128)] public string Name { get; private set; } = default!;
    [MaxLength(32)] public string Symbol { get; private set; } = default!;
    public int IntervalMinutes { get; private set; } = AlertConstants.DefaultIntervalMinutes;
    public bool Enabled { get; private set; } = true;
    public DateTimeOffset? LastEvaluatedAt { get; private set; }
    public IReadOnlyList<AlertEvent> Events => _events;

    public static AlertRule Create(UserId userId, string name, Symbol symbol, EvaluationInterval interval)
        => new()
        {
            UserId = userId,
            Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired),
            Symbol = symbol.Value,
            IntervalMinutes = interval.Minutes,
            Enabled = true
        };

    public void SetInterval(EvaluationInterval interval)
    {
        IntervalMinutes = interval.Minutes;
    }

    public void Rename(string name)
    {
        Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired);
    }

    public void SetSymbol(Symbol symbol)
    {
        Symbol = symbol.Value;
    }

    public void Enable()
    {
        Enabled = true;
    }

    public void Disable()
    {
        Enabled = false;
    }

    public void MarkEvaluated(DateTimeOffset now)
    {
        LastEvaluatedAt = now;
    }

    public AlertEvent Raise(AlertSeverity severity, string message, DateTimeOffset now)
    {
        if (!Enabled) throw new DomainException(DomainErrors.AlertRuleDisabled);
        var alertEvent = AlertEvent.Create(Id, UserId, severity, message);
        _events.Add(alertEvent);
        LastEvaluatedAt = now;

        RaiseDomainEvent(new AlertRaised(Id, UserId, severity.Value));
        return alertEvent;
    }
}

public class AlertEvent : AuditedEntity<AlertEventId>
{
    public AlertRuleId RuleId { get; private set; }
    public AlertRule Rule { get; private set; } = default!;
    public UserId UserId { get; private set; }
    [MaxLength(16)] public string Severity { get; private set; } = AlertConstants.SeverityInfo;
    public string Message { get; private set; } = string.Empty;
    public bool Acknowledged { get; private set; }

    public static AlertEvent Create(AlertRuleId ruleId, UserId userId, AlertSeverity severity, string message)
        => new()
        {
            RuleId = ruleId,
            UserId = userId,
            Severity = severity.Value,
            Message = message ?? string.Empty
        };

    public void Acknowledge()
    {
        Acknowledged = true;
    }
}

// ---------------- Prop-firm guardian (exposure caps + auto-flatten) ----------------

public class PropRule : AuditedEntity<PropRuleId>
{
    public UserId UserId { get; private set; }
    public AppUser User { get; private set; } = default!;
    public TradingAccountId TradingAccountId { get; private set; }
    public TradingAccount TradingAccount { get; private set; } = default!;
    [MaxLength(128)] public string Name { get; private set; } = default!;
    public int MaxConcurrentLiveInstances { get; private set; } = 3;
    public double DailyLossLimit { get; private set; }
    public double MaxDrawdownPercent { get; private set; }
    public bool AutoFlatten { get; private set; }
    public bool Enabled { get; private set; } = true;
    public DateTimeOffset? LastFlattenedAt { get; private set; }

    public static PropRule Create(UserId userId, TradingAccountId tradingAccountId, string name,
        int maxConcurrentLiveInstances, double dailyLossLimit, double maxDrawdownPercent, bool autoFlatten)
    {
        DomainGuard.AgainstOutOfInclusiveRange(maxConcurrentLiveInstances, 0,
            PropGuardConstants.MaxConcurrentCap, DomainErrors.MaxConcurrentOutOfRange);
        DomainGuard.AgainstNegative(dailyLossLimit, DomainErrors.DrawdownOutOfRange);
        return new PropRule
        {
            UserId = userId,
            TradingAccountId = tradingAccountId,
            Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired),
            MaxConcurrentLiveInstances = maxConcurrentLiveInstances,
            DailyLossLimit = dailyLossLimit,
            MaxDrawdownPercent = maxDrawdownPercent,
            AutoFlatten = autoFlatten,
            Enabled = true
        };
    }

    public void Update(string name, int maxConcurrentLiveInstances, double dailyLossLimit,
        double maxDrawdownPercent, bool autoFlatten, bool enabled)
    {
        DomainGuard.AgainstOutOfInclusiveRange(maxConcurrentLiveInstances, 0,
            PropGuardConstants.MaxConcurrentCap, DomainErrors.MaxConcurrentOutOfRange);
        DomainGuard.AgainstNegative(dailyLossLimit, DomainErrors.DrawdownOutOfRange);
        Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired);
        MaxConcurrentLiveInstances = maxConcurrentLiveInstances;
        DailyLossLimit = dailyLossLimit;
        MaxDrawdownPercent = maxDrawdownPercent;
        AutoFlatten = autoFlatten;
        Enabled = enabled;
    }

    public void RecordFlattened(DateTimeOffset now)
    {
        LastFlattenedAt = now;
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
    }
}

// ---------------- Access: MCP / Audit / Settings ----------------

public class McpApiKey : AuditedEntity<McpApiKeyId>
{
    public UserId UserId { get; private set; }
    public AppUser User { get; private set; } = default!;
    [MaxLength(64)] public string KeyPrefix { get; private set; } = default!;
    [MaxLength(128)] public string KeyHash { get; private set; } = default!;
    [MaxLength(128)] public string Label { get; private set; } = default!;
    public DateTimeOffset? LastUsedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static McpApiKey Create(UserId userId, string keyPrefix, string keyHash, string label)
        => new()
        {
            UserId = userId,
            KeyPrefix = keyPrefix,
            KeyHash = keyHash,
            Label = DomainGuard.AgainstNullOrWhiteSpace(label, DomainErrors.NameRequired)
        };

    public void MarkUsed(DateTimeOffset now)
    {
        LastUsedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null) throw new DomainException(DomainErrors.McpKeyAlreadyRevoked);
        RevokedAt = now;

        RaiseDomainEvent(new McpApiKeyRevoked(Id, UserId));
    }
}

public class AuditLog
{
    public long Id { get; private set; }
    public UserId? UserId { get; private set; }
    public DateTimeOffset Time { get; private set; }
    [MaxLength(64)] public string Action { get; private set; } = default!;
    [MaxLength(64)] public string EntityType { get; private set; } = default!;
    public Guid? EntityId { get; private set; }
    [MaxLength(45)] public string? Ip { get; private set; }
    public string? DetailsJson { get; private set; }

    // Tamper-evidence: each entry chains to the previous entry's hash, so any edit or deletion of a past
    // record is detectable by re-walking the chain (see IAuditTrailVerifier).
    [MaxLength(64)] public string? PrevHash { get; private set; }
    [MaxLength(64)] public string? Hash { get; private set; }

    public static AuditLog Record(string action, string entityType, DateTimeOffset now, UserId? userId = null,
        Guid? entityId = null, string? ip = null, string? detailsJson = null)
        => new()
        {
            Action = action,
            EntityType = entityType,
            UserId = userId,
            EntityId = entityId,
            Ip = ip,
            DetailsJson = detailsJson,
            Time = now
        };

    public string ComputeHash(string? prevHash)
    {
        PrevHash = prevHash;
        Hash = ComputeHashOf(prevHash, Time, Action, EntityType, UserId, EntityId, DetailsJson);
        return Hash;
    }

    public string ExpectedHash(string? prevHash) =>
        ComputeHashOf(prevHash, Time, Action, EntityType, UserId, EntityId, DetailsJson);

    private static string ComputeHashOf(string? prevHash, DateTimeOffset time, string action, string entityType,
        UserId? userId, Guid? entityId, string? detailsJson)
    {
        var canonical = $"{prevHash}|{time:O}|{action}|{entityType}|{userId?.Value}|{entityId}|{detailsJson}";
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
    }
}

public class AppSetting
{
    [MaxLength(64)] public string Key { get; private set; } = default!;
    public string Value { get; private set; } = default!;
    public DateTimeOffset UpdatedAt { get; private set; }

    public static AppSetting Create(string key, string value, DateTimeOffset now)
        => new() {Key = key, Value = value, UpdatedAt = now};

    public void SetValue(string value, DateTimeOffset now)
    {
        Value = value;
        UpdatedAt = now;
    }
}
