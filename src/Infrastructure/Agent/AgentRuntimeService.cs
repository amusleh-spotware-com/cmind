using Core;
using Core.Agent;
using Core.Ai;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Agent;

/// <summary>
/// Drives running agents 24/7. Each tick, for every Running agent and each managed account, it reads the
/// deterministic account state, asks the decision engine for a move, passes it through the safety gate
/// (<see cref="IAgentDecisionProcessor"/>), records an append-only <see cref="AgentDecisionRecord"/>, and
/// halts or executes as the gate directs. Fully fault-isolated (a failure in one agent never touches
/// another or the host), gated on AI availability, and inert unless <c>App:Ai:AgentRuntimeEnabled</c> is
/// set — so it is safe by default and cannot act without both the key and an explicit opt-in.
/// </summary>
public sealed class AgentRuntimeService(
    IServiceScopeFactory scopes, TimeProvider time, IConfiguration config) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue<bool>("App:Ai:AgentRuntimeEnabled")) return; // safe by default: off unless opted in

        try { await Task.Delay(InitialDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch { /* never let a tick failure take down the host */ }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var ai = sp.GetRequiredService<IAiFeatureService>();
        if (!ai.Enabled) return; // no key → agents stay idle; open risk stays managed by deterministic rails

        var db = sp.GetRequiredService<DataContext>();
        var engine = sp.GetRequiredService<IAgentDecisionEngine>();
        var store = sp.GetRequiredService<IAccountStateStore>();
        var executor = sp.GetRequiredService<IAgentOrderExecutor>();
        var processor = sp.GetRequiredService<IAgentDecisionProcessor>();
        var memory = sp.GetRequiredService<IAgentMemory>();

        var running = await db.TradingAgents.Where(a => a.Status == AgentStatus.Running).ToListAsync(ct);
        foreach (var agent in running)
        {
            try { await StepAgentAsync(agent, db, engine, store, executor, processor, memory, ai.Enabled, ct); }
            catch { /* isolate: one agent's failure never affects another */ }
        }
    }

    private async Task StepAgentAsync(
        TradingAgent agent, DataContext db, IAgentDecisionEngine engine, IAccountStateStore store,
        IAgentOrderExecutor executor, IAgentDecisionProcessor processor, IAgentMemory memory, bool aiAvailable, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var accountId in agent.ManagedAccounts)
        {
            var state = await store.GetStateAsync(accountId, ct);
            var decision = await engine.DecideAsync(agent, state, ct);
            var processed = processor.Process(agent, state, decision, aiAvailable, hardGoalBreached: false);

            var record = AgentDecisionRecord.Create(agent.Id, agent.UserId, agent.Watermark + 1, decision, processed);
            db.AgentDecisionRecords.Add(record);

            if (processed.ShouldHalt)
            {
                agent.Halt(processed.Reason, now);
            }
            else if (processed is { ShouldExecute: true, Order: { } order })
            {
                if (await executor.ExecuteAsync(agent, order, ct)) record.MarkExecuted();
            }

            agent.RecordAction($"{processed.Outcome}: {processed.Reason}", now);
            await db.SaveChangesAsync(ct);

            await memory.RememberAsync(agent.Id, agent.UserId, MemoryTier.LowLevelReflection,
                $"{processed.Outcome}: {decision.Reasoning}", ct);

            if (agent.Status != AgentStatus.Running) break; // halted → stop touching this agent
        }
    }
}
