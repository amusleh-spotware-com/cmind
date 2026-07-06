namespace Core.Constants;

public static class AuthPolicies
{
    public const string Owner = nameof(Owner);
    public const string AdminOrAbove = nameof(AdminOrAbove);
    public const string UserOrAbove = nameof(UserOrAbove);
}

public static class AuthSchemes
{
    public const string McpKey = nameof(McpKey);
    public const string Bearer = "Bearer";
    public const string McpTokenPrefix = "ctw_mcp_";
}

public static class EncryptionPurposes
{
    public const string CtidPassword = "ctid.password";
    public const string CbotAlgo = "cbot.algo";
    public const string CbotSource = "cbot.source";
    public const string NodeSshKey = "node.ssh.key";
    public const string NodeSshPassphrase = "node.ssh.pass";
}

public static class ConfigSections
{
    public const string Ctw = "Ctw";
}

public static class ConnectionStrings
{
    public const string CtwDb = "ctwdb";
}

public static class HubRoutes
{
    public const string Logs = "/hubs/logs";
}

public static class HealthEndpoints
{
    public const string Health = "/health";
    public const string Alive = "/alive";
    public const string LiveTag = "live";
}

public static class DockerLabels
{
    public const string User = "ctw.user";
    public const string Instance = "ctw.instance";
    public const string Type = "ctw.type";
}

public static class FilePaths
{
    public const string ContainerWorkMount = "/mnt/work";
    public const string ContainerDataDir = "/mnt/work/data";
    public const string CbotAlgoFile = "cbot.algo";
    public const string ParamsCbotsetFile = "params.cbotset";
    public const string CtidPwdFile = "ctid.pwd";
    public const string ReportJsonFile = "report.json";
    public const string ReportHtmlFile = "report.html";
    public static readonly string BuildWorkRootDefault =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ctw", "builds");
    public static readonly string LocalRunWorkRootDefault =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ctw", "local-runs");
    public const string CtwDataRootPrefix = "/var/ctw/";
}

public static class LocalNodeDefaults
{
    public const string Name = "local";
    public const int MaxInstances = 5;
}

public static class DockerImages
{
    public const string CtraderConsole = "ghcr.io/spotware/ctrader-console";
    public const string CsharpBuildDefault = "mcr.microsoft.com/dotnet/sdk:8.0";
    public const string DefaultTag = "latest";
}

public static class CliFlags
{
    public const string Ctid = "--ctid";
    public const string PwdFile = "--pwd-file";
    public const string Account = "--account";
    public const string Symbol = "--symbol";
    public const string Period = "--period";
    public const string DataDir = "--data-dir";
    public const string Start = "--start";
    public const string End = "--end";
    public const string DataMode = "--data-mode";
    public const string ReportJson = "--report-json";
    public const string Report = "--report";
    public const string ExitOnStop = "--exit-on-stop";
}

public static class BacktestDefaults
{
    public const string DataMode = "m1";
    public const string DateFormat = "dd/MM/yyyy HH:mm";
}

public static class CliCommands
{
    public const string Run = "run";
    public const string Backtest = "backtest";
}

public static class DockerCommands
{
    public const string RunDetached = "run -d";
    public const string Stop = "docker stop";
    public const string RemoveForce = "docker rm -f";
    public const string LogsFollow = "docker logs -f";
    public const string StatsNoStream = "docker stats --no-stream --format '{{.CPUPerc}}|{{.MemUsage}}'";
    public const string RunBuild = "run --rm --memory=2g --cpus=2";
    public const string NameFlag = "--name";
    public const string LabelFlag = "--label";
    public const string VolumeFlag = "-v";
    public const string ContainerNamePrefix = "ctw-";
    public const string BuildCommand = "sh -c \"cd /work && dotnet build -c Release -o /work/out 2>&1\"";
    public const string BuildOutDir = "out";
    public const string BuildMount = "/work";
    public const string AlgoExtensionPattern = "*.algo";
}
