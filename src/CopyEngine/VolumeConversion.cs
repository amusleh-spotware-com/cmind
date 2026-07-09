namespace CopyEngine;

/// <summary>
/// Converts between cTrader Open API wire volume (int64) and lots using a symbol's lot size (also
/// wire-scaled). cTrader expresses both order volume and lot size in the same "cents" unit, so the
/// scale cancels: lots = volume / lotSize. Pure and deterministic.
/// </summary>
public static class VolumeConversion
{
    public static double LotsFromProtocol(long protocolVolume, long lotSize)
        => lotSize <= 0 ? 0 : (double)protocolVolume / lotSize;

    public static long ProtocolFromLots(double lots, long lotSize)
        => lotSize <= 0 ? 0 : (long)Math.Round(lots * lotSize, MidpointRounding.AwayFromZero);
}
