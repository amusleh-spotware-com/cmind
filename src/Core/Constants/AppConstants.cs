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
    public const string BuildWorkRootDefault = "/var/ctw/builds";
    public const string CtwDataRootPrefix = "/var/ctw/";
}

public static class DockerImages
{
    public const string CtraderConsole = "ghcr.io/spotware/ctrader-console";
    public const string CsharpBuildDefault = "mcr.microsoft.com/dotnet/sdk:9.0";
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
    public const string ReportJson = "--report-json";
    public const string Report = "--report";
    public const string ExitOnStop = "--exit-on-stop";
}

public static class CliCommands
{
    public const string Run = "run";
    public const string Backtest = "backtest";
}
