using System.ComponentModel;
using System.Security.Claims;
using Core;
using Core.Ai;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Mcp.Tools;

[McpServerToolType]
public sealed class AiTools(DataContext db, IHttpContextAccessor http, IAiFeatureService ai)
{
    private UserId? CurrentUserId => Guid.TryParse(
        http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? UserId.From(g) : null;

    [McpServerTool, Description("Generate a cTrader cBot from a natural-language strategy description.")]
    public async Task<object> GenerateCBot(
        [Description("Language: CSharp or Python")] string language,
        [Description("Strategy description")] string description)
    {
        var r = await ai.GenerateCBotAsync(language, description, default);
        return new { r.Success, r.Text, r.Error };
    }

    [McpServerTool, Description("Review cBot source code for correctness bugs and trading risks.")]
    public async Task<object> ReviewCBot(
        [Description("Language: CSharp or Python")] string language,
        [Description("cBot source code")] string source)
    {
        var r = await ai.ReviewCBotAsync(language, source, default);
        return new { r.Success, r.Text, r.Error };
    }

    [McpServerTool, Description("Get current market sentiment and event risk for a trading symbol.")]
    public async Task<object> MarketSentiment([Description("Symbol, e.g. EURUSD")] string symbol)
    {
        var r = await ai.MarketSentimentAsync(symbol, default);
        return new { r.Success, r.Text, r.Error };
    }

    [McpServerTool, Description("Analyze the completed backtest report for one of the user's instances.")]
    public async Task<object> AnalyzeBacktest([Description("Instance ID")] Guid instanceId)
    {
        if (CurrentUserId is not { } uid) return new { Success = false, Error = "unauthorized" };
        var iid = InstanceId.From(instanceId);
        var bt = await db.Instances.OfType<CompletedBacktestInstance>()
            .Where(i => i.Id == iid && i.UserId == uid)
            .Select(i => new { i.ReportJson, Name = i.CBot.Name })
            .FirstOrDefaultAsync();
        if (bt is null || string.IsNullOrWhiteSpace(bt.ReportJson))
            return new { Success = false, Error = "no completed backtest report for that instance" };
        var r = await ai.AnalyzeBacktestAsync(bt.Name, bt.ReportJson!, default);
        return new { r.Success, r.Text, r.Error };
    }
}
