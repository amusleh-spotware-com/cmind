namespace Core;

public abstract record UserRole(string Name, int Rank)
{
    public sealed record OwnerRole() : UserRole(nameof(Owner), 0);
    public sealed record AdminRole() : UserRole(nameof(Admin), 1);
    public sealed record UserRoleValue() : UserRole(nameof(User), 2);
    public sealed record ViewerRole() : UserRole(nameof(Viewer), 3);

    public static readonly UserRole Owner = new OwnerRole();
    public static readonly UserRole Admin = new AdminRole();
    public static readonly UserRole User = new UserRoleValue();
    public static readonly UserRole Viewer = new ViewerRole();

    public static readonly IReadOnlyList<UserRole> All = [Owner, Admin, User, Viewer];

    public bool IsAtLeast(UserRole other) => Rank <= other.Rank;

    public static UserRole FromName(string name) =>
        All.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown role: {name}", nameof(name));

    public sealed override string ToString() => Name;
}

public abstract record NodeMode(string Name)
{
    public sealed record RunOnly() : NodeMode(nameof(Run));
    public sealed record BacktestOnly() : NodeMode(nameof(Backtest));
    public sealed record MixedMode() : NodeMode(nameof(Mixed));

    public static readonly NodeMode Run = new RunOnly();
    public static readonly NodeMode Backtest = new BacktestOnly();
    public static readonly NodeMode Mixed = new MixedMode();
    public static readonly IReadOnlyList<NodeMode> All = [Run, Backtest, Mixed];

    public static NodeMode FromName(string name) =>
        All.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown mode: {name}", nameof(name));

    public sealed override string ToString() => Name;
}

public abstract record NodeStatus(string Name)
{
    public sealed record ActiveStatus() : NodeStatus(nameof(Active));
    public sealed record DecommissioningStatus() : NodeStatus(nameof(Decommissioning));
    public sealed record UnreachableStatus() : NodeStatus(nameof(Unreachable));

    public static readonly NodeStatus Active = new ActiveStatus();
    public static readonly NodeStatus Decommissioning = new DecommissioningStatus();
    public static readonly NodeStatus Unreachable = new UnreachableStatus();
    public static readonly IReadOnlyList<NodeStatus> All = [Active, Decommissioning, Unreachable];

    public static NodeStatus FromName(string name) =>
        All.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown status: {name}", nameof(name));

    public sealed override string ToString() => Name;
}

public abstract record InstanceType(string Name)
{
    public sealed record RunType() : InstanceType(nameof(Run));
    public sealed record BacktestType() : InstanceType(nameof(Backtest));
    public sealed record BuildType() : InstanceType(nameof(Build));

    public static readonly InstanceType Run = new RunType();
    public static readonly InstanceType Backtest = new BacktestType();
    public static readonly InstanceType Build = new BuildType();
    public static readonly IReadOnlyList<InstanceType> All = [Run, Backtest, Build];

    public static InstanceType FromName(string name) =>
        All.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown type: {name}", nameof(name));

    public sealed override string ToString() => Name;
}

public abstract record InstanceStatus(string Name, bool IsTerminal, bool IsActive)
{
    public sealed record PendingStatus() : InstanceStatus(nameof(Pending), false, false);
    public sealed record ScheduledStatus() : InstanceStatus(nameof(Scheduled), false, true);
    public sealed record StartingStatus() : InstanceStatus(nameof(Starting), false, true);
    public sealed record RunningStatus() : InstanceStatus(nameof(Running), false, true);
    public sealed record StoppingStatus() : InstanceStatus(nameof(Stopping), false, true);
    public sealed record StoppedStatus() : InstanceStatus(nameof(Stopped), true, false);
    public sealed record CompletedStatus() : InstanceStatus(nameof(Completed), true, false);
    public sealed record FailedStatus() : InstanceStatus(nameof(Failed), true, false);

    public static readonly InstanceStatus Pending = new PendingStatus();
    public static readonly InstanceStatus Scheduled = new ScheduledStatus();
    public static readonly InstanceStatus Starting = new StartingStatus();
    public static readonly InstanceStatus Running = new RunningStatus();
    public static readonly InstanceStatus Stopping = new StoppingStatus();
    public static readonly InstanceStatus Stopped = new StoppedStatus();
    public static readonly InstanceStatus Completed = new CompletedStatus();
    public static readonly InstanceStatus Failed = new FailedStatus();

    public static readonly IReadOnlyList<InstanceStatus> All =
        [Pending, Scheduled, Starting, Running, Stopping, Stopped, Completed, Failed];

    public static InstanceStatus FromName(string name) =>
        All.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown status: {name}", nameof(name));

    public sealed override string ToString() => Name;
}

public abstract record CBotLanguage(string Name, string FileExtension)
{
    public sealed record CSharpLanguage() : CBotLanguage(nameof(CSharp), ".cs");
    public sealed record PythonLanguage() : CBotLanguage(nameof(Python), ".py");

    public static readonly CBotLanguage CSharp = new CSharpLanguage();
    public static readonly CBotLanguage Python = new PythonLanguage();
    public static readonly IReadOnlyList<CBotLanguage> All = [CSharp, Python];

    public static CBotLanguage FromName(string name) =>
        All.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown language: {name}", nameof(name));

    public sealed override string ToString() => Name;
}
