using CopyEngine;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class VolumeConversionTests
{
    private const long EurUsdLotSize = 10_000_000; // 100,000 units in cents

    [Fact]
    public void One_lot_round_trips()
    {
        VolumeConversion.LotsFromProtocol(EurUsdLotSize, EurUsdLotSize).Should().Be(1);
        VolumeConversion.ProtocolFromLots(1, EurUsdLotSize).Should().Be(EurUsdLotSize);
    }

    [Fact]
    public void Fractional_lots_convert_both_ways()
    {
        VolumeConversion.ProtocolFromLots(0.5, EurUsdLotSize).Should().Be(5_000_000);
        VolumeConversion.LotsFromProtocol(2_500_000, EurUsdLotSize).Should().Be(0.25);
    }

    [Fact]
    public void Non_positive_lot_size_yields_zero()
    {
        VolumeConversion.LotsFromProtocol(1000, 0).Should().Be(0);
        VolumeConversion.ProtocolFromLots(1, 0).Should().Be(0);
    }
}
