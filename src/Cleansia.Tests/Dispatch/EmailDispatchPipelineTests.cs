using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Azure.Storage.Queues;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// The request-path guarantees for the async account/reset email, asserted at the
/// pipeline-integration level (the same composition the resolved MediatR pipeline runs):
///   • a transport outage never fails the command — the email is enqueued post-commit and a dispatch
///     failure is logged and swallowed, so the command still returns success;
///   • a commit throw or a validation failure dispatches nothing — the buffer is discarded.
/// </summary>
public class EmailDispatchPipelineTests
{
    public sealed record FakeCommand : IRequest<BusinessResult>;

    private const string UserId = "USER-1";
    private const string CodeHash = "hash-1";

    private readonly Mock<IQueueClient> _queueClient = new();

    private static IPendingDispatch NewBuffer() => new InMemoryPendingDispatch();

    private async Task<BusinessResult> RunPipeline(
        IPendingDispatch pending, IUnitOfWork uow, Func<Task<BusinessResult>> handler)
    {
        var dispatch = new PostCommitDispatchBehavior<FakeCommand, BusinessResult>(
            pending, _queueClient.Object,
            NullLogger<PostCommitDispatchBehavior<FakeCommand, BusinessResult>>.Instance);
        var unitOfWork = new UnitOfWorkPipelineBehavior<FakeCommand, BusinessResult>(uow);

        return await dispatch.Handle(
            new FakeCommand(),
            _ => unitOfWork.Handle(new FakeCommand(), _ => handler(), CancellationToken.None),
            CancellationToken.None);
    }

    private static void EnqueueEmail(IPendingDispatch pending)
    {
        var key = MessageKeys.Email(EmailType.ConfirmationEmail, UserId, CodeHash);
        pending.Enqueue(
            QueueNames.SendEmail,
            new QueueEnvelope<SendEmailMessage>(
                key, "tenant-1",
                new SendEmailMessage(EmailType.ConfirmationEmail, "user@example.com", "John Doe", "raw-code", "en", UserId)),
            key);
    }

    [Fact]
    public async Task Transport_Outage_Does_Not_Fail_The_Command()
    {
        var pending = NewBuffer();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Simulate a transport outage at dispatch time — the send onto the wire blows up.
        _queueClient
            .Setup(q => q.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("transport unreachable"));

        var result = await RunPipeline(pending, uow.Object, () =>
        {
            EnqueueEmail(pending);
            return Task.FromResult(BusinessResult.Success());
        });

        // The committed operation succeeds; the dispatch failure is swallowed (never a 500).
        Assert.True(result.IsSuccess);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task On_Commit_Throw_No_Email_Is_Dispatched()
    {
        var pending = NewBuffer();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("commit failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunPipeline(pending, uow.Object, () =>
            {
                EnqueueEmail(pending);
                return Task.FromResult(BusinessResult.Success());
            }));

        _queueClient.Verify(
            q => q.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task On_Validation_Failure_No_Email_Is_Dispatched()
    {
        var pending = NewBuffer();
        var uow = new Mock<IUnitOfWork>();

        var failure = BusinessResult.Failure(new Error("auth.existing_user", "rejected"));
        var result = await RunPipeline(pending, uow.Object, () => Task.FromResult(failure));

        Assert.True(result.IsFailure);
        uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _queueClient.Verify(
            q => q.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task On_Commit_Success_Email_Is_Dispatched_Once()
    {
        var pending = NewBuffer();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _queueClient
            .Setup(q => q.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await RunPipeline(pending, uow.Object, () =>
        {
            EnqueueEmail(pending);
            return Task.FromResult(BusinessResult.Success());
        });

        Assert.True(result.IsSuccess);
        _queueClient.Verify(
            q => q.SendAsync(QueueNames.SendEmail, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
