using Core;
using Core.Domain;
using Core.PropFirm;
using FluentAssertions;
using Infrastructure.PropFirm;
using Microsoft.Extensions.Logging;
using Xunit;

namespace UnitTests.PropFirm;

public class PropFirmAlertNotifierTests
{
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Records.Add((logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    [Fact]
    public async Task Notifier_logs_pass_breach_and_warning()
    {
        var logger = new CapturingLogger<PropFirmAlertNotifier>();
        var notifier = new PropFirmAlertNotifier(logger);
        var challengeId = PropFirmChallengeId.New();
        var userId = UserId.New();

        await notifier.HandleAsync(new PropFirmChallengePassed(challengeId, userId), default);
        await notifier.HandleAsync(new PropFirmChallengeBreached(challengeId, userId, BreachReason.DailyLoss), default);
        await notifier.HandleAsync(new PropFirmDrawdownWarning(challengeId, userId, 82.5), default);

        logger.Records.Should().HaveCount(3);
        logger.Records.Should().Contain(r => r.Level == LogLevel.Information && r.Message.Contains("PASSED"));
        logger.Records.Should().Contain(r => r.Level == LogLevel.Warning && r.Message.Contains("FAILED"));
        logger.Records.Should().Contain(r => r.Level == LogLevel.Warning && r.Message.Contains("drawdown"));
    }
}
