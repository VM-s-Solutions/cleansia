using Cleansia.Config.Validation;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Azure.Storage.Queues;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// TC-DISPATCH-0 (ADR-0002 verify #7 / AC1 + AC2) — the F2/SEC-W1 fix, asserted at the
/// pipeline-integration level: <see cref="UnitOfWorkPipelineBehavior{TRequest,TResponse}"/> and
/// <see cref="PostCommitDispatchBehavior{TRequest,TResponse}"/> wired together (outer = dispatch,
/// inner = UoW) over a shared scoped <see cref="IPendingDispatch"/> buffer, exercising the three
/// branches the contract freezes:
///   • commit SUCCESS  → buffer drained EXACTLY once, each message dispatched via
///     <see cref="IQueueClient.SendAsync"/>, STRICTLY AFTER the commit (AC1).
///   • commit THROWS    → next() propagates, the dispatch guard is never reached, NO SendAsync,
///     the buffer is discarded (AC2 — the F2 fix: no message on the wire for a rolled-back write).
///   • validation FAILS → the handler never runs, the buffer is empty, nothing is dispatched (AC2).
///
/// AC8 (verify #4, pipeline-order) asserts the concrete registration order
/// PostCommitDispatch → Validation → UnitOfWork so a future re-swap cannot resurrect F11 or
/// before-commit dispatch.
///
/// Test-first (RED until PostCommitDispatchBehavior + IPendingDispatch + InMemoryPendingDispatch
/// exist and the behavior is registered outermost). Pairs with T-0127; merges with the fix.
/// </summary>
public class PostCommitDispatchBehaviorTests
{
    public sealed record FakeCommand : IRequest<BusinessResult>;

    private readonly Mock<IQueueClient> _queueClient = new();

    private static IPendingDispatch NewBuffer() => new InMemoryPendingDispatch();

    private PostCommitDispatchBehavior<FakeCommand, BusinessResult> Dispatch(IPendingDispatch pending) =>
        new(pending, _queueClient.Object, NullLogger<PostCommitDispatchBehavior<FakeCommand, BusinessResult>>.Instance);

    private static UnitOfWorkPipelineBehavior<FakeCommand, BusinessResult> UnitOfWork(IUnitOfWork uow) =>
        new(uow);

    // Compose dispatch (outer) → uow (inner) → handler, sharing the same pending buffer, the way the
    // resolved MediatR pipeline runs them.
    private async Task<BusinessResult> RunPipeline(
        IPendingDispatch pending,
        IUnitOfWork uow,
        Func<Task<BusinessResult>> handler)
    {
        var dispatch = Dispatch(pending);
        var unitOfWork = UnitOfWork(uow);

        return await dispatch.Handle(
            new FakeCommand(),
            _ => unitOfWork.Handle(new FakeCommand(), _ => handler(), CancellationToken.None),
            CancellationToken.None);
    }

    // ── AC1 — commit success drains exactly once, after the commit ─────────────────────

    [Fact]
    public async Task On_Commit_Success_Buffer_Is_Drained_And_Each_Message_Sent_Once_After_Commit()
    {
        var pending = NewBuffer();
        var uow = new Mock<IUnitOfWork>();
        var committed = false;
        uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => committed = true)
            .Returns(Task.CompletedTask);

        // The dispatcher sends the already-serialized envelope BODY (a string) verbatim through the
        // existing IQueueClient (PendingMessage.Body is a serialized QueueEnvelope<T> — ADR D1 record).
        var sendObservedCommitState = (bool?)null;
        _queueClient
            .Setup(q => q.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => sendObservedCommitState = committed)
            .Returns(Task.CompletedTask);

        var result = await RunPipeline(pending, uow.Object, () =>
        {
            pending.Enqueue(
                QueueNames.GenerateReceipt,
                new QueueEnvelope<GenerateReceiptMessage>(
                    MessageKeys.Receipt("ORDER-1"), "tenant-1", new GenerateReceiptMessage("ORDER-1", "en")),
                MessageKeys.Receipt("ORDER-1"));
            return Task.FromResult(BusinessResult.Success());
        });

        Assert.True(result.IsSuccess);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _queueClient.Verify(
            q => q.SendAsync(QueueNames.GenerateReceipt, It.Is<string>(b => b.Contains("ORDER-1")), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.True(sendObservedCommitState, "Dispatch (SendAsync) must run STRICTLY AFTER the UoW commit.");
    }

    // ── AC2 — commit throw → no dispatch, buffer discarded (the F2 fix) ─────────────────

    [Fact]
    public async Task On_Commit_Throw_Nothing_Is_Dispatched()
    {
        var pending = NewBuffer();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("commit blew up (e.g. parallel-retry 23505)"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunPipeline(pending, uow.Object, () =>
            {
                pending.Enqueue(
                    QueueNames.GenerateReceipt,
                    new QueueEnvelope<GenerateReceiptMessage>(
                        MessageKeys.Receipt("ORDER-1"), "tenant-1", new GenerateReceiptMessage("ORDER-1", "en")),
                    MessageKeys.Receipt("ORDER-1"));
                return Task.FromResult(BusinessResult.Success());
            }));

        _queueClient.Verify(
            q => q.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // The behavior did NOT drain on the throw — the message sits undispatched in the buffer (the
        // real scope is then disposed per request, dropping it; D1.2). The contract point is that the
        // guard was never reached, so the dispatcher was never invoked (asserted above).
        Assert.Single(pending.Drain());
    }

    // ── AC2 — validation failure (handler never ran, empty buffer) → nothing dispatched ─

    [Fact]
    public async Task On_Validation_Failure_Nothing_Is_Dispatched()
    {
        var pending = NewBuffer();
        var uow = new Mock<IUnitOfWork>();

        // The inner pipeline returns a failure BusinessResult WITHOUT the handler running, so the
        // buffer is empty and the UoW (defense-in-depth) never commits.
        var failure = BusinessResult.Failure(new Error("test.rejected", "rejected by validator"));
        var result = await RunPipeline(pending, uow.Object, () => Task.FromResult(failure));

        Assert.True(result.IsFailure);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _queueClient.Verify(
            q => q.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── AC2 — committed success but empty buffer (short-circuit) → drains to nothing ────

    [Fact]
    public async Task On_Success_With_Empty_Buffer_No_Dispatch()
    {
        // The HandlePaymentNotification already-processed short-circuit returns success with an empty
        // pending buffer (D1.2). Drain → nothing → no SendAsync.
        var pending = NewBuffer();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await RunPipeline(pending, uow.Object, () => Task.FromResult(BusinessResult.Success()));

        Assert.True(result.IsSuccess);
        _queueClient.Verify(
            q => q.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── D1.1 — Enqueue is idempotent within a request on (QueueName, MessageKey) ────────

    [Fact]
    public async Task Duplicate_Enqueue_Same_Key_Dispatches_Once()
    {
        var pending = NewBuffer();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var key = MessageKeys.Receipt("ORDER-1");
        var result = await RunPipeline(pending, uow.Object, () =>
        {
            var envelope = new QueueEnvelope<GenerateReceiptMessage>(key, "tenant-1", new GenerateReceiptMessage("ORDER-1", "en"));
            pending.Enqueue(QueueNames.GenerateReceipt, envelope, key);
            pending.Enqueue(QueueNames.GenerateReceipt, envelope, key); // same (queue, key) → one buffered
            return Task.FromResult(BusinessResult.Success());
        });

        Assert.True(result.IsSuccess);
        _queueClient.Verify(
            q => q.SendAsync(QueueNames.GenerateReceipt, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── AC8 / verify #4 — pipeline order: PostCommitDispatch → Validation → UnitOfWork ──

    [Fact]
    public void PostCommitDispatch_Is_Registered_Outermost_Before_Validation_And_UnitOfWork()
    {
        var services = new ServiceCollection().AddValidators();

        var behaviorTypes = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(d => d.ImplementationType)
            .ToList();

        var dispatchIndex = behaviorTypes.IndexOf(typeof(PostCommitDispatchBehavior<,>));
        var validationIndex = behaviorTypes.IndexOf(typeof(ValidationPipelineBehavior<,>));
        var unitOfWorkIndex = behaviorTypes.IndexOf(typeof(UnitOfWorkPipelineBehavior<,>));

        Assert.True(dispatchIndex >= 0, "PostCommitDispatchBehavior must be registered.");
        Assert.True(validationIndex >= 0, "ValidationPipelineBehavior must be registered.");
        Assert.True(unitOfWorkIndex >= 0, "UnitOfWorkPipelineBehavior must be registered.");
        Assert.True(
            dispatchIndex < validationIndex,
            "ADR-0002 D4: PostCommitDispatchBehavior must be OUTERMOST (registered before Validation).");
        Assert.True(
            validationIndex < unitOfWorkIndex,
            "ADR-0002 D4: Validation must be outer to UnitOfWork (registered before it).");
    }
}
