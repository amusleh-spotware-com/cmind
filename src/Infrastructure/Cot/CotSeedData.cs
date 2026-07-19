using Core;
using Core.Cot;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Cot;

/// <summary>
/// The curated catalog of the most-watched CFTC contract markets — their stable contract codes, asset-class
/// grouping, and (where unambiguous) the tradeable symbol they map to. Seeded on startup so the UI and the
/// bounded ingestion have a working market list out of the box; the same code set is what the weekly worker
/// pulls, keeping ingestion volume small and predictable. Idempotent: an existing market (e.g. one ingestion
/// created unmapped) is upgraded to the curated group/symbol rather than duplicated.
/// </summary>
public static class CotSeedData
{
    public sealed record Seed(string Code, string Name, string Exchange, CotContractGroup Group, string? Symbol);

    public static IReadOnlyList<Seed> Markets { get; } =
    [
        // FX majors
        new("099741", "Euro FX", "CME", CotContractGroup.Fx, "EURUSD"),
        new("096742", "British Pound", "CME", CotContractGroup.Fx, "GBPUSD"),
        new("097741", "Japanese Yen", "CME", CotContractGroup.Fx, "USDJPY"),
        new("092741", "Swiss Franc", "CME", CotContractGroup.Fx, "USDCHF"),
        new("090741", "Canadian Dollar", "CME", CotContractGroup.Fx, "USDCAD"),
        new("232741", "Australian Dollar", "CME", CotContractGroup.Fx, "AUDUSD"),
        new("112741", "New Zealand Dollar", "CME", CotContractGroup.Fx, "NZDUSD"),
        new("098662", "U.S. Dollar Index", "ICE", CotContractGroup.Fx, null),
        // Metals
        new("088691", "Gold", "COMEX", CotContractGroup.Metals, "XAUUSD"),
        new("084691", "Silver", "COMEX", CotContractGroup.Metals, "XAGUSD"),
        new("085692", "Copper", "COMEX", CotContractGroup.Metals, null),
        new("076651", "Platinum", "NYMEX", CotContractGroup.Metals, null),
        new("075651", "Palladium", "NYMEX", CotContractGroup.Metals, null),
        // Energy
        new("067651", "WTI Crude Oil", "NYMEX", CotContractGroup.Energy, null),
        new("023651", "Natural Gas", "NYMEX", CotContractGroup.Energy, null),
        // Rates
        new("020601", "UST Bond", "CBOT", CotContractGroup.Rates, null),
        new("043602", "UST 10Y Note", "CBOT", CotContractGroup.Rates, null),
        // Equity indices
        new("13874A", "E-mini S&P 500", "CME", CotContractGroup.Indices, null),
        new("20974+", "Nasdaq-100 Consolidated", "CME", CotContractGroup.Indices, null),
        new("12460+", "DJIA Consolidated", "CBOT", CotContractGroup.Indices, null),
        new("239742", "Russell 2000 E-mini", "CME", CotContractGroup.Indices, null),
        new("1170E1", "VIX Futures", "CFE", CotContractGroup.Indices, null),
        // Crypto
        new("133741", "Bitcoin", "CME", CotContractGroup.Crypto, "BTCUSD"),
        new("146021", "Ether", "CME", CotContractGroup.Crypto, "ETHUSD"),
        // Grains
        new("002602", "Corn", "CBOT", CotContractGroup.Agriculture, null),
        new("001602", "Wheat SRW", "CBOT", CotContractGroup.Agriculture, null),
        new("005602", "Soybeans", "CBOT", CotContractGroup.Agriculture, null),
        // Softs
        new("080732", "Sugar No. 11", "ICE", CotContractGroup.Softs, null),
        new("083731", "Coffee C", "ICE", CotContractGroup.Softs, null),
        new("033661", "Cotton No. 2", "ICE", CotContractGroup.Softs, null),
        new("073732", "Cocoa", "ICE", CotContractGroup.Softs, null)
    ];

    public static IReadOnlyList<string> TrackedCodes { get; } = Markets.Select(m => m.Code).ToList();

    public static async Task EnsureSeededAsync(DataContext db, CancellationToken ct)
    {
        var codes = TrackedCodes.ToArray();
        var existing = await db.CotMarkets.Where(m => codes.Contains(m.ContractCodeValue)).ToListAsync(ct);
        var byCode = existing.ToDictionary(m => m.ContractCodeValue, StringComparer.Ordinal);
        var changed = false;

        foreach (var seed in Markets)
        {
            var symbol = seed.Symbol is null ? (Symbol?)null : new Symbol(seed.Symbol);
            if (byCode.TryGetValue(seed.Code, out var market))
            {
                market.Rename(seed.Name);
                market.Reclassify(seed.Group);
                market.MapSymbol(symbol);
            }
            else
            {
                db.CotMarkets.Add(CotMarket.Create(
                    new ContractMarketCode(seed.Code), seed.Name, seed.Exchange, seed.Group, symbol));
            }

            changed = true;
        }

        if (changed) await db.SaveChangesAsync(ct);
    }
}
