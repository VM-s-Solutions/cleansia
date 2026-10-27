using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// Two timers carry several commands only to save a trigger, so a command that throws must not cost the
/// others their tick: the stale-checkout timer's third command is the only sweep that refunds paid orders
/// nobody took, its fourth completes the crew jobs everyone marked done, and the recurring timer's second
/// re-drives stuck card refunds. Each command is sent from
/// a scope of its own, which takes a thrown command's change tracker and tenant override away with it,
/// and the invocation still fails once the last command has run, because the host's error middleware is
/// what makes a thrown timer visible.
///
/// <para>The handlers are resolved the way the Functions host resolves them: registered scoped and taken
/// from the invocation's own scope. Counting the scopes that resolve a mediator therefore counts the
/// scopes the commands ran in.</para>
/// </summary>
public sealed class TimerCommandIsolationTests
{
    private static readonly Regex SendCall = new(@"\bSend\(", RegexOptions.Compiled);

    private readonly Mock<IMediator> _mediator = new();
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _log = [];
    private int _mediatorScopes;

    [Fact]
    public async Task A_Throwing_Stale_Checkout_Sweep_Still_Sends_The_Others_Then_Escapes()
    {
        StaleCheckoutCommandsSucceed();
        var thrown = new InvalidOperationException("stale checkout sweep");
        _mediator
            .Setup(m => m.Send(It.IsAny<CleanupStalePendingOrders.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(thrown);

        var escaped = await RunStaleCheckoutTimerAsync();

        Assert.Same(thrown, escaped);
        _mediator.Verify(
            m => m.Send(It.IsAny<ReleaseOrphanedBenefitReservations.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(
            m => m.Send(It.IsAny<CancelUnfilledOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(
            m => m.Send(It.IsAny<AutoCompleteMarkedDoneOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_Throwing_Middle_Command_Still_Lets_The_Others_Run_Then_Escapes()
    {
        StaleCheckoutCommandsSucceed();
        var thrown = new InvalidOperationException("orphaned reservation release");
        _mediator
            .Setup(m => m.Send(
                It.IsAny<ReleaseOrphanedBenefitReservations.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(thrown);

        var escaped = await RunStaleCheckoutTimerAsync();

        Assert.Same(thrown, escaped);
        _mediator.Verify(
            m => m.Send(It.IsAny<CleanupStalePendingOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(
            m => m.Send(It.IsAny<CancelUnfilledOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(
            m => m.Send(It.IsAny<AutoCompleteMarkedDoneOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Two_Throwing_Commands_Escape_Together_After_All_Four_Ran()
    {
        StaleCheckoutCommandsSucceed();
        var first = new InvalidOperationException("stale checkout sweep");
        var third = new InvalidOperationException("unfilled order sweep");
        _mediator
            .Setup(m => m.Send(It.IsAny<CleanupStalePendingOrders.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(first);
        _mediator
            .Setup(m => m.Send(It.IsAny<CancelUnfilledOrders.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(third);

        var escaped = await RunStaleCheckoutTimerAsync();

        var together = Assert.IsType<AggregateException>(escaped);
        Assert.Equal(new Exception[] { first, third }, together.InnerExceptions);
        _mediator.Verify(
            m => m.Send(It.IsAny<CleanupStalePendingOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(
            m => m.Send(It.IsAny<ReleaseOrphanedBenefitReservations.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(
            m => m.Send(It.IsAny<CancelUnfilledOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(
            m => m.Send(It.IsAny<AutoCompleteMarkedDoneOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_Thrown_Command_Is_Logged_Once_At_Warning_Naming_It()
    {
        StaleCheckoutCommandsSucceed();
        var thrown = new InvalidOperationException("orphaned reservation release");
        _mediator
            .Setup(m => m.Send(
                It.IsAny<ReleaseOrphanedBenefitReservations.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(thrown);

        await RunStaleCheckoutTimerAsync();

        var logged = Assert.Single(_log, e => e.Level >= LogLevel.Warning);
        Assert.Equal(LogLevel.Warning, logged.Level);
        Assert.Same(thrown, logged.Exception);
        Assert.Contains(nameof(ReleaseOrphanedBenefitReservations), logged.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Host_Stopping_Ends_The_Stale_Checkout_Tick_At_Once()
    {
        StaleCheckoutCommandsSucceed();
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();
        _mediator
            .Setup(m => m.Send(It.IsAny<CleanupStalePendingOrders.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(stopping.Token));

        var escaped = await RunStaleCheckoutTimerAsync(stopping.Token);

        Assert.IsType<OperationCanceledException>(escaped);
        _mediator.Verify(
            m => m.Send(It.IsAny<ReleaseOrphanedBenefitReservations.Command>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediator.Verify(
            m => m.Send(It.IsAny<CancelUnfilledOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediator.Verify(
            m => m.Send(It.IsAny<AutoCompleteMarkedDoneOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_Cancellation_The_Host_Did_Not_Ask_For_Fails_Only_Its_Own_Command()
    {
        StaleCheckoutCommandsSucceed();
        var timedOut = new TaskCanceledException("a call inside the sweep timed out");
        _mediator
            .Setup(m => m.Send(It.IsAny<CleanupStalePendingOrders.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(timedOut);

        var escaped = await RunStaleCheckoutTimerAsync();

        Assert.Same(timedOut, escaped);
        _mediator.Verify(
            m => m.Send(It.IsAny<ReleaseOrphanedBenefitReservations.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(
            m => m.Send(It.IsAny<CancelUnfilledOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(
            m => m.Send(It.IsAny<AutoCompleteMarkedDoneOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task The_Stale_Checkout_Timer_Sends_Each_Of_Its_Four_Commands_From_A_Scope_Of_Its_Own()
    {
        StaleCheckoutCommandsSucceed();

        var escaped = await RunStaleCheckoutTimerAsync();

        Assert.Null(escaped);
        Assert.Equal(4, _mediatorScopes);
    }

    [Fact]
    public async Task A_Throwing_Recurring_Auto_Cancel_Still_Re_Drives_Refunds_Then_Escapes()
    {
        RecurringCommandsSucceed();
        var thrown = new InvalidOperationException("recurring auto-cancel");
        _mediator
            .Setup(m => m.Send(It.IsAny<AutoCancelStaleRecurringOrders.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(thrown);

        var escaped = await RunRecurringTimerAsync();

        Assert.Same(thrown, escaped);
        _mediator.Verify(
            m => m.Send(It.IsAny<RedrivePendingRefunds.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Both_Recurring_Commands_Throwing_Escape_Together()
    {
        RecurringCommandsSucceed();
        var autoCancel = new InvalidOperationException("recurring auto-cancel");
        var redrive = new InvalidOperationException("refund re-drive");
        _mediator
            .Setup(m => m.Send(It.IsAny<AutoCancelStaleRecurringOrders.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(autoCancel);
        _mediator
            .Setup(m => m.Send(It.IsAny<RedrivePendingRefunds.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(redrive);

        var escaped = await RunRecurringTimerAsync();

        var together = Assert.IsType<AggregateException>(escaped);
        Assert.Equal(new Exception[] { autoCancel, redrive }, together.InnerExceptions);
    }

    [Fact]
    public async Task Host_Stopping_Ends_The_Recurring_Tick_At_Once()
    {
        RecurringCommandsSucceed();
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();
        _mediator
            .Setup(m => m.Send(It.IsAny<AutoCancelStaleRecurringOrders.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(stopping.Token));

        var escaped = await RunRecurringTimerAsync(stopping.Token);

        Assert.IsType<OperationCanceledException>(escaped);
        _mediator.Verify(
            m => m.Send(It.IsAny<RedrivePendingRefunds.Command>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task The_Recurring_Timer_Sends_Each_Of_Its_Two_Commands_From_A_Scope_Of_Its_Own()
    {
        RecurringCommandsSucceed();

        var escaped = await RunRecurringTimerAsync();

        Assert.Null(escaped);
        Assert.Equal(2, _mediatorScopes);
    }

    [Fact]
    public void Every_Functions_Handler_Sending_Several_Commands_Gives_Each_A_Scope_Of_Its_Own()
    {
        var handlersDirectory = Path.Combine(RequireSolutionDirectory(), "Cleansia.Functions.Core", "Handlers");
        var handlers = Directory.EnumerateFiles(handlersDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(file => (Name: Path.GetFileName(file), Text: File.ReadAllText(file)))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(handlers);

        var severalCommands = handlers
            .Select(f => (f.Name, f.Text, Sends: SendCall.Matches(f.Text).Count))
            .Where(f => f.Sends >= 2)
            .ToList();

        var offenders = severalCommands
            .Where(f => !f.Text.Contains("CreateAsyncScope", StringComparison.Ordinal))
            .Select(f => $"{f.Name} ({f.Sends} sends)")
            .ToList();

        Assert.True(offenders.Count == 0,
            "A Functions handler sends several commands from one shared scope. There a thrown command ends "
            + "the tick for every command after it, and its change tracker and tenant override ride into the "
            + "next one. Send each from its own `await using var scope = scopeFactory.CreateAsyncScope();`, log "
            + "a thrown one at Warning naming it, and rethrow once after the last, as "
            + "CleanupStalePendingOrdersHandler does. Offenders:\n  " + string.Join("\n  ", offenders));

        Assert.True(
            severalCommands.Any(f => f.Name == "CleanupStalePendingOrdersHandler.cs" && f.Sends == 4),
            "The walk no longer finds CleanupStalePendingOrdersHandler.cs with its four commands, so this "
            + "guard could pass on no multi-command handler at all. Either the call's text changed and the "
            + "count must learn it, or the handler moved and this anchor must move with it.");
    }

    private void StaleCheckoutCommandsSucceed()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<CleanupStalePendingOrders.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new CleanupStalePendingOrders.Response(CancelledCount: 0)));
        _mediator
            .Setup(m => m.Send(
                It.IsAny<ReleaseOrphanedBenefitReservations.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new ReleaseOrphanedBenefitReservations.Response(0)));
        _mediator
            .Setup(m => m.Send(It.IsAny<CancelUnfilledOrders.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new CancelUnfilledOrders.Response(0, 0, 0)));
        _mediator
            .Setup(m => m.Send(It.IsAny<AutoCompleteMarkedDoneOrders.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new AutoCompleteMarkedDoneOrders.Response(0, 0)));
    }

    private void RecurringCommandsSucceed()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<AutoCancelStaleRecurringOrders.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new AutoCancelStaleRecurringOrders.Response(Cancelled: 0, Considered: 0)));
        _mediator
            .Setup(m => m.Send(It.IsAny<RedrivePendingRefunds.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new RedrivePendingRefunds.Response(Considered: 0, Redriven: 0, Alerted: 0)));
    }

    private Task<Exception?> RunStaleCheckoutTimerAsync(CancellationToken ct = default) =>
        RunAsync<CleanupStalePendingOrdersHandler>(handler => handler.HandleAsync(ct));

    private Task<Exception?> RunRecurringTimerAsync(CancellationToken ct = default) =>
        RunAsync<AutoCancelStaleRecurringOrdersHandler>(handler => handler.HandleAsync(ct));

    private async Task<Exception?> RunAsync<THandler>(Func<THandler, Task> handle) where THandler : notnull
    {
        var services = new ServiceCollection();
        services.AddScoped<IMediator>(_ =>
        {
            _mediatorScopes++;
            return _mediator.Object;
        });
        services.AddSingleton<ILogger<CleanupStalePendingOrdersHandler>>(
            new CapturingLogger<CleanupStalePendingOrdersHandler>(_log));
        services.AddSingleton<ILogger<AutoCancelStaleRecurringOrdersHandler>>(
            new CapturingLogger<AutoCancelStaleRecurringOrdersHandler>(_log));
        services.AddScoped<CleanupStalePendingOrdersHandler>();
        services.AddScoped<AutoCancelStaleRecurringOrdersHandler>();

        await using var provider = services.BuildServiceProvider();
        await using var invocation = provider.CreateAsyncScope();
        var handler = invocation.ServiceProvider.GetRequiredService<THandler>();
        return await Record.ExceptionAsync(() => handle(handler));
    }

    private static string RequireSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }

        Assert.Fail("Could not locate the solution directory from the test base directory.");
        return string.Empty;
    }

    private sealed class CapturingLogger<T>(List<(LogLevel Level, string Message, Exception? Exception)> entries)
        : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add((logLevel, formatter(state, exception), exception));
    }
}
