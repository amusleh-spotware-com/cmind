using System.Text;
using Core;
using Core.Constants;
using Core.CopyTrading;
using Core.Domain;
using CTraderOpenApi.Client;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public record CreateCopyProfileRequest(string Name, Guid SourceAccountId, IReadOnlyList<Guid>? DestinationAccountIds = null);

public record AddCopyDestinationRequest(
    Guid DestinationAccountId,
    MoneyManagementMode Mode,
    double Parameter,
    double SlippagePips,
    int MaxDelaySeconds,
    bool Reverse,
    bool CopyStopLoss,
    bool CopyTakeProfit,
    CopyDirectionFilter Direction,
    double MinLot,
    double MaxLot,
    bool ForceMinLot,
    double MaxDrawdownPercent,
    double DailyLossLimit,
    SymbolFilterMode SymbolFilterMode = SymbolFilterMode.None,
    IReadOnlyList<string>? SymbolFilters = null,
    IReadOnlyList<SymbolMapPair>? SymbolMap = null,
    bool MirrorPartialClose = true,
    bool MirrorScaleIn = false,
    bool CopyPendingOrders = false,
    bool CopyTrailingStop = false,
    CopyOrderTypes OrderTypes = CopyOrderTypes.All,
    bool CopyPendingExpiry = true,
    bool CopyMasterSlippage = true,
    double LotSanityAbsoluteMaxLots = 0,
    double LotSanityMasterMultiple = 0,
    bool ManageOnly = false,
    bool SyncOpenOnStart = true,
    bool SyncClosedOnStart = true,
    int TradingHoursStartMinuteUtc = 0,
    int TradingHoursEndMinuteUtc = 0,
    string? SourceLabelFilter = null,
    AccountProtectionMode AccountProtectionMode = AccountProtectionMode.Off,
    double AccountProtectionStopEquity = 0,
    double? AccountProtectionTakeEquity = null,
    double PropRuleDailyLossCap = 0,
    double PropRuleTrailingDrawdown = 0,
    double ConsistencyThresholdPercent = 0,
    int ExecutionJitterMaxMs = 0,
    double RiskFallbackLots = 0,
    double PerformanceFeePercent = 0);

public record SymbolMapPair(string Source, string Destination, double VolumeMultiplier = 1);

// Edit of an existing destination's settings. Carries only the fields the copy-profile editor exposes;
// the advanced per-destination settings (manage-only, account protection, prop rules, sync policy, trading
// hours, jitter, fees, lot-sanity) are left untouched so editing the basics never resets them.
public record UpdateCopyDestinationRequest(
    MoneyManagementMode Mode,
    double Parameter,
    double SlippagePips,
    int MaxDelaySeconds,
    bool Reverse,
    bool CopyStopLoss,
    bool CopyTakeProfit,
    CopyDirectionFilter Direction,
    double MinLot,
    double MaxLot,
    bool ForceMinLot,
    double MaxDrawdownPercent,
    double DailyLossLimit,
    SymbolFilterMode SymbolFilterMode = SymbolFilterMode.None,
    IReadOnlyList<string>? SymbolFilters = null,
    IReadOnlyList<SymbolMapPair>? SymbolMap = null,
    bool MirrorPartialClose = true,
    bool MirrorScaleIn = false,
    bool CopyPendingOrders = false,
    bool CopyTrailingStop = false,
    CopyOrderTypes OrderTypes = CopyOrderTypes.All,
    bool CopyPendingExpiry = true,
    bool CopyMasterSlippage = true);

public record ChangeCopySourceRequest(Guid SourceAccountId);

public record LockCopyDestinationRequest(int Minutes);

public record PublishProviderRequest(string DisplayName, string? Description = null, double PerformanceFeePercent = 0);

public static class CopyEndpoints
{
    public static IEndpointRouteBuilder MapCopyEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/copy").RequireAuthorization(AuthPolicies.UserOrAbove)
            .RequireFeature(Core.Features.FeatureFlag.CopyTrading);

        g.MapGet("/profiles", async (DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var profiles = await db.CopyProfiles.Include(p => p.Destinations)
                .Where(p => p.UserId == uid).ToListAsync();
            return profiles.Select(p => new
            {
                p.Id,
                p.Name,
                SourceAccountId = p.SourceAccountId.Value,
                Status = p.Status.ToString(),
                DestinationCount = p.Destinations.Count
            });
        });

        g.MapGet("/accounts/{tradingAccountId:guid}/symbols", async (Guid tradingAccountId, DataContext db,
            ICurrentUser u, ISecretProtector p, IOpenApiClient client, CancellationToken ct) =>
        {
            var uid = u.UserId!.Value;
            var tid = TradingAccountId.From(tradingAccountId);
            var account = await db.TradingAccounts.Include(t => t.CTid)
                .FirstOrDefaultAsync(t => t.Id == tid && t.CTid.UserId == uid, ct);
            if (account?.OpenApiAuthorizationId is null || account.CtidTraderAccountId is null)
                return Results.BadRequest("Account is not Open API linked.");

            var auth = await db.OpenApiAuthorizations.FirstOrDefaultAsync(a => a.Id == account.OpenApiAuthorizationId, ct);
            var application = auth is null ? null
                : await db.OpenApiApplications.FirstOrDefaultAsync(a => a.Id == auth.ApplicationId, ct);
            if (auth is null || application is null) return Results.NotFound();

            var secret = Encoding.UTF8.GetString(p.Unprotect(application.EncryptedClientSecret, EncryptionPurposes.OpenApiClientSecret));
            var token = Encoding.UTF8.GetString(p.Unprotect(auth.EncryptedAccessToken, EncryptionPurposes.OpenApiAccessToken));
            try
            {
                return Results.Ok(await client.GetSymbolNamesAsync(
                    account.IsLive, application.ClientId, secret, token, account.CtidTraderAccountId.Value, ct));
            }
            catch (Exception)
            {
                return Results.Ok(Array.Empty<string>());
            }
        });

        g.MapGet("/profiles/{id:guid}", async (Guid id, ICopyProfileRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var profile = await repo.GetWithDestinationsAsync(CopyProfileId.From(id), ct);
            if (profile is null || profile.UserId != u.UserId!.Value) return Results.NotFound();
            return Results.Ok(new
            {
                profile.Id,
                profile.Name,
                SourceAccountId = profile.SourceAccountId.Value,
                Status = profile.Status.ToString(),
                Destinations = profile.Destinations.Select(d => new
                {
                    d.Id,
                    DestinationAccountId = d.DestinationAccountId.Value,
                    Mode = d.RiskMode.ToString(),
                    d.RiskParameter,
                    d.SlippagePips,
                    d.MaxDelaySeconds,
                    d.Reverse,
                    d.CopyStopLoss,
                    d.CopyTakeProfit,
                    d.MirrorPartialClose,
                    d.MirrorScaleIn,
                    d.CopyPendingOrders,
                    d.CopyTrailingStop,
                    OrderTypes = d.CopyOrderTypes.ToString(),
                    d.CopyPendingExpiry,
                    d.CopyMasterSlippage,
                    Direction = d.Direction.ToString(),
                    d.MinLot,
                    d.MaxLot,
                    d.ForceMinLot,
                    d.MaxDrawdownPercent,
                    d.DailyLossLimit,
                    d.LotSanityAbsoluteMaxLots,
                    d.LotSanityMasterMultiple,
                    d.ManageOnly,
                    d.SyncOpenOnStart,
                    d.SyncClosedOnStart,
                    d.TradingHoursStartMinuteUtc,
                    d.TradingHoursEndMinuteUtc,
                    d.SourceLabelFilter,
                    AccountProtectionMode = d.AccountProtectionMode.ToString(),
                    d.AccountProtectionStopEquity,
                    d.AccountProtectionTakeEquity,
                    d.PropRuleDailyLossCap,
                    d.PropRuleTrailingDrawdown,
                    d.ConsistencyThresholdPercent,
                    d.ExecutionJitterMaxMs,
                    d.RiskFallbackLots,
                    d.PerformanceFeePercent,
                    d.HighWaterMarkEquity,
                    SymbolFilterMode = d.SymbolFilterMode.ToString(),
                    SymbolFilters = d.SymbolFilters.Select(f => f.Symbol),
                    SymbolMaps = d.SymbolMaps.Select(m => new { m.Source, m.Destination, m.VolumeMultiplier })
                })
            });
        });

        g.MapPost("/profiles", async (CreateCopyProfileRequest req, ICopyProfileRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var profile = CopyProfile.Create(uid, req.Name, TradingAccountId.From(req.SourceAccountId));
            if (req.DestinationAccountIds is { Count: > 0 })
                foreach (var destinationId in req.DestinationAccountIds.Distinct())
                    profile.AddDestination(TradingAccountId.From(destinationId), RiskSettings.Default);
            await repo.AddAsync(profile, ct);
            await repo.SaveChangesAsync(ct);
            return Results.Ok(new { profile.Id });
        }).RequireConsent(Core.LegalDocumentType.RiskDisclosure);

        g.MapDelete("/profiles/{id:guid}", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            var pid = CopyProfileId.From(id);
            var profile = await db.CopyProfiles.FirstOrDefaultAsync(p => p.Id == pid && p.UserId == u.UserId!.Value, ct);
            if (profile is null) return Results.NotFound();
            db.CopyProfiles.Remove(profile);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPut("/profiles/{id:guid}/source", async (Guid id, ChangeCopySourceRequest req,
            ICopyProfileRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var profile = await repo.GetWithDestinationsAsync(CopyProfileId.From(id), ct);
            if (profile is null || profile.UserId != u.UserId!.Value) return Results.NotFound();
            profile.ChangeSource(TradingAccountId.From(req.SourceAccountId));
            await repo.SaveChangesAsync(ct);
            return Results.Ok(new { SourceAccountId = profile.SourceAccountId.Value });
        });

        g.MapPost("/profiles/{id:guid}/destinations", async (Guid id, AddCopyDestinationRequest req,
            ICopyProfileRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var profile = await repo.GetWithDestinationsAsync(CopyProfileId.From(id), ct);
            if (profile is null || profile.UserId != u.UserId!.Value) return Results.NotFound();

            var destination = profile.AddDestination(
                TradingAccountId.From(req.DestinationAccountId), new RiskSettings(req.Mode, req.Parameter));
            destination.ConfigureSlippage(new SlippagePips(req.SlippagePips));
            destination.ConfigureMaxDelay(MaxCopyDelay.Seconds(req.MaxDelaySeconds));
            destination.ConfigureBounds(new LotBounds(req.MinLot, req.MaxLot, req.ForceMinLot));
            destination.SetReverse(req.Reverse);
            destination.SetCopyProtection(req.CopyStopLoss, req.CopyTakeProfit);
            destination.SetDirection(req.Direction);
            destination.SetPartialCloseMirroring(req.MirrorPartialClose, req.MirrorScaleIn);
            destination.SetPendingOrderCopying(req.CopyPendingOrders);
            destination.SetTrailingStopCopying(req.CopyTrailingStop);
            destination.SetOrderTypeFilter(req.OrderTypes);
            destination.SetExpiryCopying(req.CopyPendingExpiry);
            destination.SetSlippageCopying(req.CopyMasterSlippage);
            destination.SetGuards(new DrawdownPercent(req.MaxDrawdownPercent), req.DailyLossLimit);
            destination.ConfigureLotSanity(new LotSanityCeiling(req.LotSanityAbsoluteMaxLots, req.LotSanityMasterMultiple));
            destination.SetManageOnly(req.ManageOnly);
            destination.SetSyncPolicy(req.SyncOpenOnStart, req.SyncClosedOnStart);
            destination.ConfigureTradingHours(new TradingWindow(req.TradingHoursStartMinuteUtc, req.TradingHoursEndMinuteUtc));
            destination.SetSourceLabelFilter(req.SourceLabelFilter);
            destination.SetAccountProtection(new AccountProtectionPolicy(
                req.AccountProtectionMode, req.AccountProtectionStopEquity, req.AccountProtectionTakeEquity));
            destination.SetPropRuleGuard(new PropRuleGuard(req.PropRuleDailyLossCap, req.PropRuleTrailingDrawdown));
            destination.SetConsistencyThreshold(req.ConsistencyThresholdPercent);
            destination.SetExecutionJitter(req.ExecutionJitterMaxMs);
            destination.SetRiskFallbackLots(req.RiskFallbackLots);
            destination.SetPerformanceFee(new PerformanceFee(req.PerformanceFeePercent));
            if (req.SymbolMap is { Count: > 0 })
                destination.SetSymbolMap(req.SymbolMap.Select(m => new SymbolMapEntry(new Symbol(m.Source), new Symbol(m.Destination), m.VolumeMultiplier)));
            if (req.SymbolFilterMode != SymbolFilterMode.None && req.SymbolFilters is { Count: > 0 })
                destination.SetSymbolFilter(req.SymbolFilterMode, req.SymbolFilters.Select(s => new Symbol(s)));
            await repo.SaveChangesAsync(ct);
            return Results.Ok(new { destination.Id });
        });

        // Update an EXISTING destination's editor-exposed settings (CRUD parity — a destination you can add
        // you can also edit). Advanced settings not on the editor are intentionally left as-is.
        g.MapPut("/profiles/{id:guid}/destinations/{destinationId:guid}", async (Guid id, Guid destinationId,
            UpdateCopyDestinationRequest req, ICopyProfileRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var profile = await repo.GetWithDestinationsAsync(CopyProfileId.From(id), ct);
            if (profile is null || profile.UserId != u.UserId!.Value) return Results.NotFound();
            var destination = profile.Destinations.FirstOrDefault(d => d.Id == CopyDestinationId.From(destinationId));
            if (destination is null) return Results.NotFound();

            destination.ConfigureRisk(new RiskSettings(req.Mode, req.Parameter));
            destination.ConfigureSlippage(new SlippagePips(req.SlippagePips));
            destination.ConfigureMaxDelay(MaxCopyDelay.Seconds(req.MaxDelaySeconds));
            destination.ConfigureBounds(new LotBounds(req.MinLot, req.MaxLot, req.ForceMinLot));
            destination.SetReverse(req.Reverse);
            destination.SetCopyProtection(req.CopyStopLoss, req.CopyTakeProfit);
            destination.SetDirection(req.Direction);
            destination.SetPartialCloseMirroring(req.MirrorPartialClose, req.MirrorScaleIn);
            destination.SetPendingOrderCopying(req.CopyPendingOrders);
            destination.SetTrailingStopCopying(req.CopyTrailingStop);
            destination.SetOrderTypeFilter(req.OrderTypes);
            destination.SetExpiryCopying(req.CopyPendingExpiry);
            destination.SetSlippageCopying(req.CopyMasterSlippage);
            destination.SetGuards(new DrawdownPercent(req.MaxDrawdownPercent), req.DailyLossLimit);
            destination.SetSymbolMap(req.SymbolMap is { Count: > 0 }
                ? req.SymbolMap.Select(m => new SymbolMapEntry(new Symbol(m.Source), new Symbol(m.Destination), m.VolumeMultiplier))
                : []);
            if (req.SymbolFilterMode != SymbolFilterMode.None && req.SymbolFilters is { Count: > 0 })
                destination.SetSymbolFilter(req.SymbolFilterMode, req.SymbolFilters.Select(s => new Symbol(s)));
            else
                destination.SetSymbolFilter(SymbolFilterMode.None, []);
            await repo.SaveChangesAsync(ct);
            return Results.Ok(new { destination.Id });
        });

        g.MapGet("/profiles/{id:guid}/destinations/{destinationId:guid}/symbol-map.csv", async (Guid id, Guid destinationId,
            ICopyProfileRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var profile = await repo.GetWithDestinationsAsync(CopyProfileId.From(id), ct);
            if (profile is null || profile.UserId != u.UserId!.Value) return Results.NotFound();
            var destination = profile.Destinations.FirstOrDefault(d => d.Id == CopyDestinationId.From(destinationId));
            if (destination is null) return Results.NotFound();
            var csv = CopySymbolMapCsv.Format(destination.SymbolMaps.Select(m => (m.Source, m.Destination, m.VolumeMultiplier)));
            return Results.Text(csv, "text/csv");
        });

        g.MapPut("/profiles/{id:guid}/destinations/{destinationId:guid}/symbol-map/csv", async (Guid id, Guid destinationId,
            HttpRequest request, ICopyProfileRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var profile = await repo.GetWithDestinationsAsync(CopyProfileId.From(id), ct);
            if (profile is null || profile.UserId != u.UserId!.Value) return Results.NotFound();
            var destination = profile.Destinations.FirstOrDefault(d => d.Id == CopyDestinationId.From(destinationId));
            if (destination is null) return Results.NotFound();
            using var reader = new StreamReader(request.Body);
            var csv = await reader.ReadToEndAsync(ct);
            destination.SetSymbolMap(CopySymbolMapCsv.Parse(csv));
            await repo.SaveChangesAsync(ct);
            return Results.Ok(new { destination.SymbolMaps.Count });
        });

        g.MapDelete("/profiles/{id:guid}/destinations/{destinationId:guid}", async (Guid id, Guid destinationId,
            ICopyProfileRepository repo, ICurrentUser u, TimeProvider time, CancellationToken ct) =>
        {
            var profile = await repo.GetWithDestinationsAsync(CopyProfileId.From(id), ct);
            if (profile is null || profile.UserId != u.UserId!.Value) return Results.NotFound();
            profile.RemoveDestination(CopyDestinationId.From(destinationId), time.GetUtcNow());
            await repo.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/profiles/{id:guid}/destinations/{destinationId:guid}/lock", async (Guid id, Guid destinationId,
            LockCopyDestinationRequest req, ICopyProfileRepository repo, ICurrentUser u, TimeProvider time, CancellationToken ct) =>
        {
            var profile = await repo.GetWithDestinationsAsync(CopyProfileId.From(id), ct);
            if (profile is null || profile.UserId != u.UserId!.Value) return Results.NotFound();
            var destination = profile.Destinations.FirstOrDefault(d => d.Id == CopyDestinationId.From(destinationId));
            if (destination is null) return Results.NotFound();
            destination.LockConfig(time.GetUtcNow().AddMinutes(req.Minutes));
            await repo.SaveChangesAsync(ct);
            return Results.Ok(new { destination.ConfigLockedUntil });
        });

        // Phase 3 execution-transparency read model (CQRS-lite): query the CopyExecution log directly and
        // aggregate in memory. Returns a per-profile summary (fill rate, avg latency, avg realized slippage)
        // plus the most recent copy facts. Empty unless App:Copy:TransparencyEnabled has been populating it.
        g.MapGet("/profiles/{id:guid}/transparency", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            var owns = await db.CopyProfiles.AnyAsync(p => p.Id == CopyProfileId.From(id) && p.UserId == u.UserId!.Value, ct);
            if (!owns) return Results.NotFound();

            var rows = await db.CopyExecutions.Where(x => x.ProfileId == id)
                .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Take(500).ToListAsync(ct);

            var opened = rows.Where(x => x.Kind == CopyExecutionKind.Opened).ToList();
            var failed = rows.Count(x => x.Kind == CopyExecutionKind.Failed);
            var attempts = opened.Count + failed;
            var summary = new
            {
                Total = rows.Count,
                Opened = opened.Count,
                Failed = failed,
                FillRate = attempts == 0 ? 0d : (double)opened.Count / attempts,
                AvgLatencyMs = opened.Count == 0 ? 0d : opened.Average(x => x.LatencyMilliseconds),
                AvgSlippagePoints = opened.Where(x => x.SlippagePoints.HasValue)
                    .Select(x => (double)x.SlippagePoints!.Value).DefaultIfEmpty(0d).Average()
            };
            return Results.Ok(new
            {
                Summary = summary,
                Recent = rows.Select(x => new
                {
                    x.DestinationCtidTraderAccountId,
                    x.SourcePositionId,
                    x.Symbol,
                    Kind = x.Kind.ToString(),
                    x.IsBuy,
                    x.Volume,
                    x.MasterPrice,
                    x.SlippagePoints,
                    x.LatencyMilliseconds,
                    x.Reason,
                    x.OccurredAt
                })
            });
        });

        // Phase 4 fee report: a profile's performance-fee accruals (high-water-mark settlements) + total charged.
        g.MapGet("/profiles/{id:guid}/fees", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            var owns = await db.CopyProfiles.AnyAsync(p => p.Id == CopyProfileId.From(id) && p.UserId == u.UserId!.Value, ct);
            if (!owns) return Results.NotFound();

            var rows = await db.CopyFeeAccruals.Where(x => x.ProfileId == id)
                .OrderByDescending(x => x.SettledAt).ThenByDescending(x => x.Id).Take(500).ToListAsync(ct);
            return Results.Ok(new
            {
                TotalFee = rows.Sum(x => x.FeeAmount),
                Accruals = rows.Select(x => new
                {
                    x.DestinationId,
                    x.HighWaterMarkBefore,
                    x.Equity,
                    x.FeePercent,
                    x.FeeAmount,
                    x.SettledAt
                })
            });
        });

        // Phase 4 marketplace: publish a profile as a public provider listing (verified-live if its source
        // account trades real money), update it, or unpublish it.
        g.MapPost("/profiles/{id:guid}/publish", async (Guid id, PublishProviderRequest req, DataContext db,
            ICurrentUser u, TimeProvider time, CancellationToken ct) =>
        {
            var uid = u.UserId!.Value;
            var pid = CopyProfileId.From(id);
            var profile = await db.CopyProfiles.FirstOrDefaultAsync(p => p.Id == pid && p.UserId == uid, ct);
            if (profile is null) return Results.NotFound();

            var sourceLive = await db.TradingAccounts.Where(a => a.Id == profile.SourceAccountId)
                .Select(a => (bool?)a.IsLive).FirstOrDefaultAsync(ct) ?? false;

            var fee = new PerformanceFee(req.PerformanceFeePercent);
            var listing = await db.CopyProviderListings.FirstOrDefaultAsync(l => l.ProfileId == pid, ct);
            if (listing is null)
            {
                listing = CopyProviderListing.Create(uid, pid, req.DisplayName, req.Description, fee, sourceLive);
                listing.Publish(time.GetUtcNow());
                db.CopyProviderListings.Add(listing);
            }
            else
            {
                listing.UpdateDetails(req.DisplayName, req.Description, fee, sourceLive);
                listing.Publish(time.GetUtcNow());
            }
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Concurrent publish for the same profile hit the one-listing-per-profile unique index.
                return Results.Conflict("This profile is already being published; retry.");
            }
            return Results.Ok(new { listing.Id, listing.VerifiedLive, listing.Published });
        });

        g.MapDelete("/profiles/{id:guid}/publish", async (Guid id, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            var pid = CopyProfileId.From(id);
            var listing = await db.CopyProviderListings.FirstOrDefaultAsync(l => l.ProfileId == pid && l.UserId == u.UserId!.Value, ct);
            if (listing is null) return Results.NotFound();
            listing.Unpublish();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // Browsable provider marketplace: every published listing, ranked, with a performance summary
        // projected from the CopyExecution transparency log (fill rate, avg latency, avg realized slippage).
        g.MapGet("/marketplace", async (DataContext db, CancellationToken ct) =>
        {
            var listings = await db.CopyProviderListings.Where(l => l.Published).ToListAsync(ct);
            if (listings.Count == 0) return Results.Ok(Array.Empty<object>());

            var profileIds = listings.Select(l => l.ProfileId.Value).ToList();
            // Aggregate in SQL (GroupBy + Count/Sum with CASE) so the full execution history is never
            // materialized — a busy provider's transparency log could be millions of rows.
            var stats = (await LoadMarketplaceStatsAsync(db, profileIds, ct)).ToDictionary(s => s.ProfileId);

            var result = listings.Select(l =>
            {
                var s = stats.GetValueOrDefault(l.ProfileId.Value);
                var opened = s?.Opened ?? 0;
                var attempts = opened + (s?.Failed ?? 0);
                var fillRate = attempts == 0 ? 0d : (double)opened / attempts;
                var avgLatency = opened == 0 ? 0d : s!.LatencySum / opened;
                var slippageCount = s?.SlippageCount ?? 0;
                var avgSlippage = slippageCount == 0 ? 0d : s!.SlippageSum / slippageCount;
                return new
                {
                    l.Id,
                    ProfileId = l.ProfileId.Value,
                    l.DisplayName,
                    l.Description,
                    l.PerformanceFeePercent,
                    l.VerifiedLive,
                    l.PublishedAt,
                    Executions = s?.Total ?? 0,
                    FillRate = fillRate,
                    AvgLatencyMs = avgLatency,
                    AvgSlippagePoints = avgSlippage,
                    Score = MarketplaceScore(fillRate, avgLatency, avgSlippage, l.VerifiedLive)
                };
            }).OrderByDescending(x => x.Score).ToList();

            return Results.Ok(result);
        });

        // 2b copy notification feed: the signed-in user's recent operational notifications across all their
        // profiles (destination tripped, account-protection/prop breach, flatten), plus an unacknowledged count.
        g.MapGet("/notifications", async (DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            var uid = u.UserId!.Value;
            var rows = await db.CopyNotifications.Where(x => x.UserId == uid)
                .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Take(200).ToListAsync(ct);
            return Results.Ok(new
            {
                Unacknowledged = rows.Count(x => !x.Acknowledged),
                Items = rows.Select(x => new
                {
                    x.Id,
                    x.ProfileId,
                    x.DestinationCtidTraderAccountId,
                    Kind = x.Kind.ToString(),
                    Severity = x.Severity.ToString(),
                    x.Message,
                    x.OccurredAt,
                    x.Acknowledged
                })
            });
        });

        g.MapPost("/notifications/{notificationId:long}/acknowledge",
            async (long notificationId, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            var uid = u.UserId!.Value;
            var row = await db.CopyNotifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == uid, ct);
            if (row is null) return Results.NotFound();
            row.Acknowledge();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/profiles/{id:guid}/flatten", async (Guid id, ICopyProfileRepository repo, ICurrentUser u,
            TimeProvider time, CancellationToken ct) =>
        {
            var profile = await repo.GetByIdAsync(CopyProfileId.From(id), u.UserId!.Value, ct);
            if (profile is null) return Results.NotFound();
            profile.RequestFlatten(time.GetUtcNow());
            await repo.SaveChangesAsync(ct);
            return Results.Accepted($"/api/copy/profiles/{id}");
        });

        g.MapPost("/profiles/{id:guid}/{action}", async (Guid id, string action,
            ICopyProfileRepository repo, ICurrentUser u, CancellationToken ct) =>
        {
            var profile = await repo.GetByIdAsync(CopyProfileId.From(id), u.UserId!.Value, ct);
            if (profile is null) return Results.NotFound();
            switch (action)
            {
                case "start": profile.Start(); break;
                case "pause": profile.Pause(); break;
                case "stop": profile.Stop(); break;
                default: return Results.NotFound();
            }
            await repo.SaveChangesAsync(ct);
            return Results.Ok(new { Status = profile.Status.ToString() });
        });

        return app;
    }

    // Per-profile execution stats aggregated in the database (never materializes the execution history).
    // Internal so the SQL translation + correctness is asserted directly against real Postgres.
    internal sealed record MarketplaceProfileStats(
        Guid ProfileId, int Total, int Opened, int Failed, double LatencySum, double SlippageSum, int SlippageCount);

    internal static async Task<IReadOnlyList<MarketplaceProfileStats>> LoadMarketplaceStatsAsync(
        DataContext db, IReadOnlyList<Guid> profileIds, CancellationToken ct)
        => await db.CopyExecutions.Where(x => profileIds.Contains(x.ProfileId))
            .GroupBy(x => x.ProfileId)
            .Select(grp => new MarketplaceProfileStats(
                grp.Key,
                grp.Count(),
                grp.Count(x => x.Kind == CopyExecutionKind.Opened),
                grp.Count(x => x.Kind == CopyExecutionKind.Failed),
                grp.Sum(x => x.Kind == CopyExecutionKind.Opened ? x.LatencyMilliseconds : 0d),
                grp.Sum(x => x.SlippagePoints != null ? (double)x.SlippagePoints!.Value : 0d),
                grp.Count(x => x.SlippagePoints != null)))
            .ToListAsync(ct);

    // A 0-100 ranking score: fill rate dominates, low latency and low slippage add, a verified-live badge
    // gives a small trust bonus. Deterministic and monotonic so the marketplace ordering is stable.
    internal static double MarketplaceScore(double fillRate, double avgLatencyMs, double avgSlippagePoints, bool verifiedLive)
    {
        var latencyScore = Math.Clamp(1 - avgLatencyMs / 2000.0, 0, 1);
        var slippageScore = Math.Clamp(1 - avgSlippagePoints / 10.0, 0, 1);
        var score = fillRate * 60 + latencyScore * 20 + slippageScore * 20;
        return Math.Round(verifiedLive ? Math.Min(100, score + 10) : score, 1);
    }
}
