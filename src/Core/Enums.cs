namespace Core;

public enum UserRole { Owner = 0, Admin = 1, User = 2, Viewer = 3 }

public enum NodeMode { Run = 0, Backtest = 1, Mixed = 2 }

public enum NodeStatus { Active = 0, Decommissioning = 1, Unreachable = 2 }

public enum InstanceType { Run = 0, Backtest = 1, Build = 2 }

public enum InstanceStatus
{
    Pending = 0,
    Scheduled = 1,
    Starting = 2,
    Running = 3,
    Stopping = 4,
    Stopped = 5,
    Completed = 6,
    Failed = 7
}

public enum CBotLanguage { CSharp = 0, Python = 1 }
