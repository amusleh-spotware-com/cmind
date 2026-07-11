# src/Core — pure domain

Zero infra deps. No EF, no `HttpClient`, no Docker, no ASP.NET, no Anthropic — if you need any of
those here, you modeled it wrong: put an interface here, implement it in `Infrastructure`. Full
playbook: **`ddd-dotnet`** skill. This file = the non-negotiables.

- **Rich aggregates, never anemic.** No public setters — state changes through intention-revealing
  methods that guard invariants (`cbot.Rename(...)`, `mandate.Enable()`). Setters are `private set` /
  `init`. Invariants checked **inside** the aggregate, once, at the point of change; callers trust it.
- **Factories/ctors produce a valid state or throw a `DomainException`** (not `ArgumentException`).
  Prefer a static `Create(...)` when construction has rules; keep a private ctor for EF.
- **One aggregate = one consistency boundary.** Reference other aggregates by **strong ID**
  (`CBotId`, `NodeId`), not navigation property. Mutate children only through their root
  (`cbot.AddParamSet(...)`, never `new ParamSet` + `context.Add`).
- **Value objects over primitives.** No bare `string symbol`, `Guid id`, `double riskPercent`,
  `int minutes` crossing a boundary — wrap them (self-validating, immutable, equality by value; see
  `StrongIds.cs`). Percent/risk/money VOs reject out-of-range in their ctor.
- **Domain events** signal "something happened" (`InstanceStarted`, `BacktestCompleted`,
  `RiskThresholdBreached`); raise from aggregate methods, collect on the entity, dispatch **after**
  `SaveChanges`. Cross-aggregate reactions subscribe — never inline them into the mutating use-case.
- **Domain services** (interface here, impl outside) only for logic spanning aggregates
  (`INodeScheduler`). Repositories return **whole aggregates**; never leak `IQueryable` out of Core.
- **TPH state hierarchies** (`Instance`, `Node`) are the state pattern — keep it. A transition is a
  domain operation returning the next-state entity; centralize which-state-goes-where here, not in
  pollers.
- **Aggregate roots:** `AppUser`, `CTraderIdAccount` (owns `TradingAccount`), `CBot` (owns
  `ParamSet`), `CBotSourceProject`, `Instance`, `Node` (owns `NodeStats`), `AgentMandate` (owns
  `AgentProposal`), `AlertRule` (owns `AlertEvent`), `McpApiKey`. `AuditLog`/`AppSetting`/`InstanceLog`
  are append-only records, not aggregates.
- Modern C# 14 per root `CLAUDE.md`. Unit tests assert **invariants and transitions**, not getters.
