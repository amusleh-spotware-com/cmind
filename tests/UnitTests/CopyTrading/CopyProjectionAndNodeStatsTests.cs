using Core;
using Core.CopyTrading;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

// Mapping factories + small mutators for the copy transparency/notification/fee logs and NodeStats.
// (WS-1 Core backfill.)
public class CopyProjectionAndNodeStatsTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CopyExecution_maps_every_field_from_the_record()
    {
        var profileId = CopyProfileId.New();
        var record = new CopyExecutionRecord(profileId, 111, 222, "EURUSD", CopyExecutionKind.Opened,
            IsBuy: true, Volume: 1000, MasterPrice: 1.1, SlippagePoints: 3, LatencyMilliseconds: 12.5,
            Reason: "ok", OccurredAt: Now);

        var execution = CopyExecution.From(record);

        execution.ProfileId.Should().Be(profileId.Value);
        execution.DestinationCtidTraderAccountId.Should().Be(111);
        execution.SourcePositionId.Should().Be(222);
        execution.Symbol.Should().Be("EURUSD");
        execution.Kind.Should().Be(CopyExecutionKind.Opened);
        execution.IsBuy.Should().BeTrue();
        execution.Volume.Should().Be(1000);
        execution.MasterPrice.Should().Be(1.1);
        execution.SlippagePoints.Should().Be(3);
        execution.LatencyMilliseconds.Should().Be(12.5);
        execution.Reason.Should().Be("ok");
        execution.OccurredAt.Should().Be(Now);
    }

    [Fact]
    public void CopyNotification_maps_from_record_and_acknowledges()
    {
        var profileId = CopyProfileId.New();
        var userId = UserId.New();
        var record = new CopyNotificationRecord(profileId, 111, CopyNotificationKind.DestinationTripped,
            CopyNotificationSeverity.Warning, "paused", Now);

        var notification = CopyNotification.From(record, userId);

        notification.ProfileId.Should().Be(profileId.Value);
        notification.UserId.Should().Be(userId);
        notification.Kind.Should().Be(CopyNotificationKind.DestinationTripped);
        notification.Severity.Should().Be(CopyNotificationSeverity.Warning);
        notification.Message.Should().Be("paused");
        notification.Acknowledged.Should().BeFalse();

        notification.Acknowledge();
        notification.Acknowledged.Should().BeTrue();
    }

    [Fact]
    public void CopyFeeAccrual_records_the_high_water_mark_inputs()
    {
        var profileId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var userId = UserId.New();

        var accrual = CopyFeeAccrual.Create(profileId, destinationId, userId,
            highWaterMarkBefore: 1000, equity: 1200, feePercent: 20, feeAmount: 40, settledAt: Now);

        accrual.ProfileId.Should().Be(profileId);
        accrual.DestinationId.Should().Be(destinationId);
        accrual.UserId.Should().Be(userId);
        accrual.HighWaterMarkBefore.Should().Be(1000);
        accrual.Equity.Should().Be(1200);
        accrual.FeePercent.Should().Be(20);
        accrual.FeeAmount.Should().Be(40);
        accrual.SettledAt.Should().Be(Now);
    }

    [Fact]
    public void NodeStats_create_and_set_instance_counts()
    {
        var nodeId = NodeId.New();

        var stats = NodeStats.Create(nodeId, cpuPercent: 55, memUsedBytes: 100, memTotalBytes: 200,
            diskUsedBytes: 300, diskTotalBytes: 400, backtestDataUsedBytes: 50, now: Now);

        stats.NodeId.Should().Be(nodeId);
        stats.CpuPercent.Should().Be(55);
        stats.RunningCount.Should().Be(0);

        stats.SetInstanceCounts(runningCount: 4, backtestCount: 2, Now.AddMinutes(1));
        stats.RunningCount.Should().Be(4);
        stats.BacktestCount.Should().Be(2);
        stats.UpdatedAt.Should().Be(Now.AddMinutes(1));
    }
}
