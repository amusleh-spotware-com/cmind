namespace Core.Cot;

/// <summary>
/// Which CFTC Commitment of Traders report a row comes from. Each kind breaks open interest into a
/// different set of trader categories (see <see cref="CotReportKindExtensions.Categories"/>).
/// </summary>
public enum CotReportKind
{
    /// <summary>The classic report: Non-Commercial (large specs), Commercial (hedgers), Non-Reportable.</summary>
    Legacy,

    /// <summary>Commodity report: Producer/Merchant, Swap Dealers, Managed Money, Other Reportables.</summary>
    Disaggregated,

    /// <summary>Traders in Financial Futures: Dealer, Asset Manager, Leveraged Funds, Other Reportables.</summary>
    Tff
}

/// <summary>A trader classification within a COT report. Which subset applies depends on the report kind.</summary>
public enum CotTraderCategory
{
    // Legacy
    NonCommercial,
    Commercial,

    // Disaggregated
    ProducerMerchant,
    SwapDealer,
    ManagedMoney,

    // TFF
    Dealer,
    AssetManager,
    LeveragedFunds,

    // Disaggregated + TFF
    OtherReportable,

    // All kinds
    NonReportable
}

/// <summary>The asset class a contract market belongs to — drives UI grouping and symbol mapping.</summary>
public enum CotContractGroup
{
    Fx,
    Metals,
    Energy,
    Agriculture,
    Softs,
    Rates,
    Indices,
    Crypto,
    Other
}

/// <summary>
/// Where the current net position sits inside its historical range (the COT index). An extreme is the
/// classic contrarian signal — speculators most-net-long/short tends to precede reversals.
/// </summary>
public enum CotExtreme
{
    None,

    /// <summary>Net position near the top of its lookback range (COT index high).</summary>
    LongExtreme,

    /// <summary>Net position near the bottom of its lookback range (COT index low).</summary>
    ShortExtreme
}

/// <summary>Which trader categories each report kind reports, in canonical display order.</summary>
public static class CotReportKindExtensions
{
    private static readonly IReadOnlyList<CotTraderCategory> LegacyCategories =
    [
        CotTraderCategory.NonCommercial,
        CotTraderCategory.Commercial,
        CotTraderCategory.NonReportable
    ];

    private static readonly IReadOnlyList<CotTraderCategory> DisaggregatedCategories =
    [
        CotTraderCategory.ProducerMerchant,
        CotTraderCategory.SwapDealer,
        CotTraderCategory.ManagedMoney,
        CotTraderCategory.OtherReportable,
        CotTraderCategory.NonReportable
    ];

    private static readonly IReadOnlyList<CotTraderCategory> TffCategories =
    [
        CotTraderCategory.Dealer,
        CotTraderCategory.AssetManager,
        CotTraderCategory.LeveragedFunds,
        CotTraderCategory.OtherReportable,
        CotTraderCategory.NonReportable
    ];

    public static IReadOnlyList<CotTraderCategory> Categories(this CotReportKind kind) => kind switch
    {
        CotReportKind.Legacy => LegacyCategories,
        CotReportKind.Disaggregated => DisaggregatedCategories,
        CotReportKind.Tff => TffCategories,
        _ => LegacyCategories
    };

    /// <summary>The category whose net position is the headline "speculator" gauge for the COT index.</summary>
    public static CotTraderCategory SpeculatorCategory(this CotReportKind kind) => kind switch
    {
        CotReportKind.Legacy => CotTraderCategory.NonCommercial,
        CotReportKind.Disaggregated => CotTraderCategory.ManagedMoney,
        CotReportKind.Tff => CotTraderCategory.LeveragedFunds,
        _ => CotTraderCategory.NonCommercial
    };

    public static bool Reports(this CotReportKind kind, CotTraderCategory category)
        => kind.Categories().Contains(category);
}
