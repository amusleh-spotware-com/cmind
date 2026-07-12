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

    public Task<AiResult> AssessLiveExposureAsync(IReadOnlyList<AiInstanceContext> live, int maxTokens, CancellationToken ct)
    {
        var symbols = string.Join(", ", live.Select(i => i.Symbol ?? "?").Distinct());
        var user = new StringBuilder();
        user.AppendLine($"Currently traded symbols: {symbols}");
        user.AppendLine("Live bots:");
        foreach (var i in live)
            user.AppendLine($"- {i.CBotName} {i.Symbol ?? "?"} {i.Timeframe ?? "?"}");
        return client.CompleteAsync(new AiTextRequest(
            AiPrompts.ExposureSystem, user.ToString(), MaxTokens: maxTokens, EnableWebSearch: true), ct);
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

    public Task<AiResult> DebateStrategyAsync(string name, string language, string source, int maxTokens, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.DebateSystem,
            $"Name: {name}\nLanguage: {language}\n\nSource:\n{Clip(source)}", MaxTokens: maxTokens), ct);

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

    public Task<AiResult> AssessStrategyDecayAsync(
        string cBotName, string? previousReportJson, string latestReportJson, string currentParamsJson, int maxTokens, CancellationToken ct)
    {
        var user = new StringBuilder();
        user.AppendLine($"cBot: {cBotName}");
        user.AppendLine($"Current parameters (JSON):\n{Clip(currentParamsJson)}");
        if (!string.IsNullOrWhiteSpace(previousReportJson))
            user.AppendLine($"\nPrevious backtest report JSON:\n{Clip(previousReportJson)}");
        user.AppendLine($"\nLatest backtest report JSON:\n{Clip(latestReportJson)}");
        return client.CompleteAsync(new AiTextRequest(AiPrompts.DecaySystem, user.ToString(), MaxTokens: maxTokens), ct);
    }

    public Task<AiResult> PortfolioDigestAsync(IReadOnlyList<AiInstanceContext> portfolio, int maxTokens, CancellationToken ct)
    {
        var user = new StringBuilder("Portfolio (recent bots, one per line):\n");
        foreach (var i in portfolio)
            user.AppendLine(
                $"- {i.CBotName} [{i.Kind}] {i.Symbol ?? "?"} {i.Timeframe ?? "?"} status={i.Status}" +
                (i.Detail is null ? "" : $" {i.Detail}"));
        return client.CompleteAsync(new AiTextRequest(AiPrompts.DigestSystem, user.ToString(), MaxTokens: maxTokens), ct);
    }

    public Task<AiResult> RecommendCopyProfileAsync(string riskProfile, string sourceDescription, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.CopyProfileSystem,
            $"Follower risk profile: {Clip(riskProfile)}\n\nSource (master) account / strategy description:\n{Clip(sourceDescription)}"), ct);

    public Task<AiResult> GatherCurrencyForwardAsync(string calendarContextJson, int maxTokens, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.CurrencyForwardSystem,
            $"Point-in-time calendar actuals + surprises per currency (JSON):\n{Clip(calendarContextJson)}",
            MaxTokens: maxTokens, EnableWebSearch: true), ct);

    public Task<AiResult> ExplainCurrencyOutlookAsync(string rankingJson, string pairOutlookJson, int maxTokens, CancellationToken ct) =>
        client.CompleteAsync(new AiTextRequest(
            AiPrompts.CurrencyExplainSystem,
            $"Deterministic current ranking (JSON):\n{Clip(rankingJson)}\n\n" +
            $"Deterministic forward pair-outlook matrix (JSON):\n{Clip(pairOutlookJson)}",
            MaxTokens: maxTokens), ct);

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

    public const string ExposureSystem =
        "You are a risk analyst watching a trader's LIVE open exposure. Using current web information for each symbol " +
        "the trader is actively running a bot on, flag any adverse sentiment, breaking news, or imminent high-impact " +
        "events that argue for de-risking that specific position. For each symbol give: a one-line read and a clear " +
        "recommendation (hold / reduce / flatten) with the driver and today's date. Only raise real concerns. " +
        "Cite sources. Not financial advice.";

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

    public const string DecaySystem =
        "You are a quant reviewing whether a trading strategy's edge is decaying. Compare the previous and latest " +
        "backtest reports for the same cBot. State clearly whether performance is degrading, the key metric deltas " +
        "(net profit, win rate, drawdown, Sharpe/profit factor if present), the likely regime cause, and ONE concrete " +
        "parameter adjustment to test next. Be concise and skeptical. If there is only one report, say a baseline is needed.";

    public const string DigestSystem =
        "You are a portfolio analyst for a retail algo trader. From the list of the trader's recent and running bots, " +
        "produce a short digest: what is working vs failing, concentration and correlation risk (same symbol/base " +
        "currency across bots), over-exposure, and 2-3 concrete rebalancing or next-step actions. Group by theme, be " +
        "specific, and keep it brief. Not financial advice.";

    public const string DebateSystem =
        "You are a trading-desk committee reviewing a cBot before deployment. Argue it from three distinct roles, then " +
        "reconcile. Output exactly these sections: '## Bull case' (why it should make money), '## Bear case' (why it " +
        "will lose), '## Risk officer' (blow-up risks, position sizing, stop-loss, correlation), and '## Verdict' " +
        "(deploy / iterate / reject, with one-line reasoning). Ground every point in the actual code. Be concise and specific.";

    public const string CopyProfileSystem =
        "You are a copy-trading risk configurator. Given the follower's stated risk profile and a description of the " +
        "source (master) account/strategy, recommend safe copy-trading destination settings. Output ONLY a JSON object " +
        "and nothing else (no prose, no code fences): {\"riskMode\": one of [\"FixedLot\",\"LotMultiplier\"," +
        "\"NotionalMultiplier\",\"ProportionalBalance\",\"ProportionalEquity\",\"ProportionalFreeMargin\"," +
        "\"FixedRiskPercent\",\"FixedLeverage\",\"AutoProportional\"], \"riskParameter\": number, " +
        "\"maxDrawdownPercent\": number (0-100), \"dailyLossLimit\": number, \"direction\": one of [\"Both\"," +
        "\"LongOnly\",\"ShortOnly\"], \"copyStopLoss\": boolean, \"copyTakeProfit\": boolean, \"slippagePips\": number, " +
        "\"rationale\": string (<=2 sentences)}. Choose conservative values that match the stated risk tolerance; " +
        "protective copying (copyStopLoss/copyTakeProfit) should default to true. Not financial advice.";

    public const string CurateSystem =
        "You are a strategy marketplace curator. From the cBot source, produce compact JSON with fields: title (one line), " +
        "description (two sentences), tags (3-6), category, riskRating (low|medium|high), riskJustification.";

    public const string CurrencyForwardSystem =
        "You are a macro FX strategist gathering FORWARD-looking inputs for a deterministic currency-strength model. " +
        "You are given point-in-time CURRENT actuals + surprises per currency from an economic calendar — DO NOT re-guess " +
        "or override those; anchor on them. Using current web information, output ONLY a JSON object and nothing else " +
        "(no prose, no code fences): {\"currencies\": [{\"code\": string (ISO-4217), \"trajectory\": {\"ratePathBp\": number " +
        "(expected policy-rate change over the horizon, bp; hiking +, cutting -), \"inflationTrend\": number (negative = " +
        "moving toward target, positive = away), \"growthMomentum\": number (positive = accelerating), \"geopoliticalDelta\": " +
        "number in [-3,3] (positive = tailwind / safe-haven bid, negative = headwind: tariffs, fiscal/debt, elections)}, " +
        "\"currentGapFill\": {\"policyRate\": number?, \"cpi\": number?, \"gdpGrowth\": number?, \"unemployment\": number?, " +
        "\"realYield\": number?, \"externalVulnerability\": number?, \"politicalRisk\": number?, \"termsOfTrade\": number?} " +
        "(ONLY for figures the calendar did not provide; omit otherwise), \"dataConfidence\": \"High\"|\"Medium\"|\"Low\"}]}. " +
        "Provide an entry for EVERY currency in the calendar context. Be conservative for opaque EM/exotic data (Low confidence). " +
        "Not financial advice.";

    public const string CurrencyExplainSystem =
        "You are an FX desk strategist. You are given a DETERMINISTIC current strength ranking and a forward pair-outlook " +
        "matrix that were already computed by a model — treat every rank, bias and number as fixed ground truth; NEVER change " +
        "one. Explain in plain English why the top and bottom currencies rank where they do, and narrate 3-5 of the highest- " +
        "conviction pair calls (e.g. 'EUR/USD bullish 3M because ...') citing the drivers behind each. Note the honest caveat " +
        "that fundamentals are a medium/long-term positioning filter, not a short-term timing signal. Be concise. Not financial advice.";
}
