using System.Text;
using Core.Ai;

namespace Infrastructure.Ai;

public sealed class AiFeatureService(IAiClient client) : IAiFeatureService
{
    private const int MaxInputChars = 24000;

    public bool Enabled => client.Enabled;

    public Task<AiResult> GenerateCBotAsync(string language, string description, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.CodegenSystem(language),
            $"Strategy description:\n{Clip(description)}"), ct);

    public Task<AiResult> ReviewCBotAsync(string language, string source, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.ReviewSystem,
            $"Language: {language}\n\nSource:\n{Clip(source)}"), ct);

    public Task<AiResult> FixCBotAsync(string language, string source, string buildLog, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.FixSystem(language),
            $"Build log:\n{Clip(buildLog)}\n\nCurrent source:\n{Clip(source)}"), ct);

    public Task<AiResult> ProposeParamSetSuiteAsync(string cBotName, string currentParamsJson, int count, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.SuiteSystem(count),
            $"cBot: {cBotName}\nCount: {count}\nCurrent parameters (JSON):\n{Clip(currentParamsJson)}"), ct);

    public Task<AiResult> AnalyzeBacktestAsync(string cBotName, string reportJson, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.AnalyzeBacktestSystem,
            $"cBot: {cBotName}\n\nBacktest report JSON:\n{Clip(reportJson)}"), ct);

    public Task<AiResult> ProposeParamSetsAsync(string cBotName, string currentParamsJson, string? backtestReportJson, CancellationToken ct)
    {
        var user = new StringBuilder();
        user.AppendLine($"cBot: {cBotName}");
        user.AppendLine($"Current parameters (JSON):\n{Clip(currentParamsJson)}");
        if (!string.IsNullOrWhiteSpace(backtestReportJson))
            user.AppendLine($"\nLatest backtest report JSON:\n{Clip(backtestReportJson)}");
        return client.CompleteAsync(new AiTextRequest(AiPrompts.OptimizeSystem, user.ToString()), ct);
    }

    public Task<AiResult> PostMortemAsync(AiInstanceContext context, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.PostMortemSystem,
            $"cBot: {context.CBotName}\nKind: {context.Kind}\nStatus: {context.Status}\n" +
            $"Symbol: {context.Symbol ?? "?"}\nTimeframe: {context.Timeframe ?? "?"}\n" +
            $"Detail:\n{Clip(context.Detail ?? "(none)")}"), ct);

    public Task<AiResult> AssessRiskAsync(IReadOnlyList<AiInstanceContext> running, CancellationToken ct)
    {
        var user = new StringBuilder("Currently running bots:\n");
        foreach (var i in running)
            user.AppendLine(
                $"- {i.CBotName} [{i.Kind}] {i.Symbol ?? "?"} {i.Timeframe ?? "?"} status={i.Status}" +
                (i.Detail is null ? "" : $" {i.Detail}"));
        return client.CompleteAsync(new AiTextRequest(AiPrompts.RiskGuardSystem, user.ToString()), ct);
    }

    public Task<AiResult> AssessRiskActionsAsync(IReadOnlyList<AiInstanceContext> running, int maxTokens, CancellationToken ct)
    {
        var user = new StringBuilder("Running bots (each prefixed with its [index]):\n");
        for (var index = 0; index < running.Count; index++)
        {
            var i = running[index];
            user.AppendLine(
                $"[{index}] {i.CBotName} [{i.Kind}] {i.Symbol ?? "?"} {i.Timeframe ?? "?"} status={i.Status}" +
                (i.Detail is null ? "" : $" {i.Detail}"));
        }
        return client.CompleteAsync(new AiTextRequest(AiPrompts.RiskActionSystem, user.ToString(), MaxTokens: maxTokens), ct);
    }

    public Task<AiResult> MarketSentimentAsync(string symbol, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.SentimentSystem,
            $"Symbol: {symbol}", EnableWebSearch: true), ct);

    public Task<AiResult> AssessSymbolAlertAsync(string symbol, int maxTokens, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.AlertSystem,
            $"Symbol: {symbol}", MaxTokens: maxTokens, EnableWebSearch: true), ct);

    public Task<AiResult> VisionToStrategyAsync(AiImage chart, string? note, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.VisionSystem,
            string.IsNullOrWhiteSpace(note) ? "Describe this chart and design a cBot strategy for it." : Clip(note),
            Image: chart), ct);

    public Task<AiResult> CurateStrategyAsync(string name, string language, string source, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.CurateSystem,
            $"Name: {name}\nLanguage: {language}\n\nSource:\n{Clip(source)}"), ct);

    public Task<AiResult> ProposeAgentActionAsync(
        string cBotName, string objective, string currentParamsJson, string? lastReportJson, int maxTokens, CancellationToken ct)
    {
        var user = new StringBuilder();
        user.AppendLine($"cBot: {cBotName}");
        user.AppendLine($"Objective: {Clip(objective)}");
        user.AppendLine($"Current parameters (JSON):\n{Clip(currentParamsJson)}");
        if (!string.IsNullOrWhiteSpace(lastReportJson))
            user.AppendLine($"\nMost recent backtest report JSON:\n{Clip(lastReportJson)}");
        return client.CompleteAsync(new AiTextRequest(AiPrompts.AgentSystem, user.ToString(), MaxTokens: maxTokens), ct);
    }

    private static string Clip(string value) =>
        string.IsNullOrEmpty(value) || value.Length <= MaxInputChars ? value ?? string.Empty : value[..MaxInputChars];
}

internal static class AiPrompts
{
    public static string CodegenSystem(string language) =>
        $"You are an expert cTrader cBot developer. Generate a complete, compilable {language} cTrader cBot " +
        "using the cTrader Automate API (a class inheriting Robot). Include sound risk management " +
        "(explicit stop-loss, bounded position sizing). Output only the source code in a single code block, no prose.";

    public const string ReviewSystem =
        "You are a senior trading-systems reviewer. Review the cBot source for correctness bugs and trading risks " +
        "(missing stop-loss, unbounded position sizing, look-ahead bias, division on thin bars, missing error handling). " +
        "One finding per line: severity - problem - fix. No praise, no restating the code.";

    public static string FixSystem(string language) =>
        $"You are a cBot build-fixer. Given {language} cTrader cBot source and the compiler/build error log, " +
        "return the corrected complete source that will compile. Output only the source in a single code block, no prose.";

    public static string SuiteSystem(int count) =>
        $"You are a trading parameter optimizer. Output ONLY a JSON array of exactly {count} objects and nothing else " +
        "(no prose, no code fences). Each object has: \"name\" (string) and \"parameters\" (an object of cBot parameter " +
        "name/value pairs). Vary the parameters meaningfully so each set tests a distinct hypothesis.";

    public const string AnalyzeBacktestSystem =
        "You are a quantitative backtest analyst. From the cTrader backtest report JSON, give a concise, skeptical verdict: " +
        "key metrics, drawdown risk, overfitting red flags, regime sensitivity, and whether it is worth trading. Be specific.";

    public const string OptimizeSystem =
        "You are a trading strategy optimizer. Given a cBot's current parameters (JSON) and optional backtest results, " +
        "propose exactly 3 alternative parameter sets to backtest next. For each, output a JSON object plus a one-line " +
        "hypothesis. Vary parameters meaningfully; avoid trivial tweaks.";

    public const string PostMortemSystem =
        "You are a trading post-mortem analyst. Given an instance's status and outcome, explain the most likely causes " +
        "and concrete next actions. Be concise.";

    public const string RiskGuardSystem =
        "You are a real-time risk guard for a leveraged FX/CFD trading desk. Given the currently running bots, flag " +
        "concentration, correlation, and anomaly risks, and recommend actions. Be brief; only flag real concerns. " +
        "If nothing is concerning, say so in one line.";

    public const string RiskActionSystem =
        "You are a real-time risk guard for a leveraged FX/CFD desk. Each running bot is listed with an index [n]. " +
        "Assess concentration, correlation, drawdown, and anomaly risk. Output ONLY a JSON array and nothing else " +
        "(no prose, no code fences); include one object ONLY for each bot you judge risky: " +
        "{\"ref\": n (integer index), \"severity\": \"low\"|\"medium\"|\"high\"|\"critical\", " +
        "\"action\": \"none\"|\"stop\", \"reason\": string}. Recommend \"stop\" ONLY for critical, clearly unsafe positions. " +
        "If nothing is risky, output an empty array [].";

    public const string AlertSystem =
        "You are an FX/CFD alerting agent. Using current web information for the given symbol, decide whether there is " +
        "an actionable, time-sensitive development (major news, sharp move, imminent high-impact event) a trader should " +
        "be alerted to right now. Output ONLY a JSON object and nothing else (no prose, no code fences): " +
        "{\"alert\": boolean, \"severity\": \"info\"|\"warning\"|\"critical\", \"message\": string (one or two sentences, " +
        "cite the driver)}. Set alert=false when nothing is noteworthy. Not financial advice.";

    public const string SentimentSystem =
        "You are an FX/CFD market analyst. Using current web information, summarize sentiment, key drivers, and upcoming " +
        "event risk for the given symbol. Note today's date and cite sources. Keep it short and actionable. Not financial advice.";

    public const string VisionSystem =
        "You are a trading strategist. Describe the chart pattern/setup shown, then outline a concrete, rule-based cBot " +
        "strategy (entry, exit, stop-loss, position sizing) that captures it.";

    public const string AgentSystem =
        "You are an autonomous trading portfolio agent. Given a cBot, its objective, current parameters, and any recent " +
        "backtest result, decide ONE next parameter set to backtest that advances the objective. Output ONLY a JSON object " +
        "and nothing else (no prose, no code fences): {\"reasoning\": string (<=3 sentences explaining the change and how it " +
        "serves the objective and risk limits), \"name\": string (short label), \"parameters\": object (cBot parameter " +
        "name/value pairs)}. Respect the stated risk limits; prefer conservative, testable adjustments.";

    public const string CurateSystem =
        "You are a strategy marketplace curator. From the cBot source, produce compact JSON with fields: title (one line), " +
        "description (two sentences), tags (3-6), category, riskRating (low|medium|high), riskJustification.";
}
