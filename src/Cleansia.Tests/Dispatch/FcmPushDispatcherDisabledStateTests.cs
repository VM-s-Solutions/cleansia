using System.Reflection;
using Cleansia.Infra.Clients.Fcm;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// Characterization of <see cref="FcmPushDispatcher"/>'s unconfigured (disabled) no-op contract.
/// When neither <c>FCM:ServiceAccountJson</c> nor <c>FCM:ProjectId</c> is set, dispatch is a
/// DELIBERATE no-op: it returns <c>PushDispatchResult(0, tokenCount, [], Skipped: true)</c> so the
/// consumer ACKS rather than poison-loops, and the config-missing Warning is emitted AT MOST ONCE per
/// process (the terminal config latch), not on every dispatch.
/// </summary>
[Collection("FcmStaticState")]
public class FcmPushDispatcherDisabledStateTests
{
    private static readonly string[] Tokens = ["TOKEN-1", "TOKEN-2"];
    private static readonly Dictionary<string, string> Data = new() { ["k"] = "v" };

    public FcmPushDispatcherDisabledStateTests() => ResetStaticInitLatch();

    [Fact]
    public async Task Disabled_Config_Returns_Skipped_NoOp_With_All_Tokens_Counted_As_Failed()
    {
        var dispatcher = new FcmPushDispatcher(new StubFcmConfig(), new CapturingLogger());

        var result = await dispatcher.SendAsync(Tokens, "order.confirmed", Data, CancellationToken.None);

        Assert.True(result.Skipped);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(Tokens.Length, result.FailureCount);
        Assert.Empty(result.InvalidTokens);
    }

    [Fact]
    public async Task Empty_Token_List_Returns_Empty_NoOp_Without_Touching_Init()
    {
        var logger = new CapturingLogger();
        var dispatcher = new FcmPushDispatcher(new StubFcmConfig(), logger);

        var result = await dispatcher.SendAsync([], "order.confirmed", Data, CancellationToken.None);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Empty(result.InvalidTokens);
        Assert.False(result.Skipped);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public async Task Consecutive_Disabled_Dispatches_Log_The_Config_Warning_At_Most_Once()
    {
        var logger = new CapturingLogger();
        var dispatcher = new FcmPushDispatcher(new StubFcmConfig(), logger);

        var first = await dispatcher.SendAsync(Tokens, "order.confirmed", Data, CancellationToken.None);
        var second = await dispatcher.SendAsync(Tokens, "order.started", Data, CancellationToken.None);

        Assert.True(first.Skipped);
        Assert.True(second.Skipped);
        Assert.Single(logger.Warnings);
    }

    private static void ResetStaticInitLatch()
    {
        var type = typeof(FcmPushDispatcher);
        type.GetField("_initAttempted", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, false);
        type.GetField("_messaging", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, null);
    }

    private sealed class StubFcmConfig : IFcmConfig
    {
        public string ServiceAccountJson { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
    }

    private sealed class CapturingLogger : ILogger<FcmPushDispatcher>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
