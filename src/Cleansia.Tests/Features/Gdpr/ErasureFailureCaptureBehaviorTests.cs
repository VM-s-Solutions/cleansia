using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// The behavior that puts a failed erasure on record. It fires only once the deletion service has
/// marked the attempt as begun — a refusal before the walk (a blocking order, a validation reject) is a
/// business answer with no row — and then for every failed outcome the pipeline can produce: a throw
/// from the walk or from the single commit (the innermost cause, then rethrown) and a refusal on a retry
/// (the key). The sink is best-effort: its own failure never changes what the caller gets.
/// </summary>
public sealed class ErasureFailureCaptureBehaviorTests
{
    public sealed record EraseCommand : IRequest<BusinessResult>;

    private readonly Mock<IGdprDeletionFailureSink> _sink = new();
    private readonly ErasureAttempt _attempt = new();

    private ErasureFailureCaptureBehavior<EraseCommand, BusinessResult> Behavior() =>
        new(_attempt, _sink.Object, NullLogger<ErasureFailureCaptureBehavior<EraseCommand, BusinessResult>>.Instance);

    private static RequestHandlerDelegate<BusinessResult> Returns(BusinessResult result) => _ => Task.FromResult(result);

    private static RequestHandlerDelegate<BusinessResult> Throws(Exception exception) => _ => throw exception;

    private void Begun() => _attempt.Begin("subject-1", "request-1", "jana.novakova@example.com");

    [Fact]
    public async Task A_Throw_After_The_Walk_Began_Writes_The_Failed_Row_With_The_Root_Cause_And_Rethrows()
    {
        var commitThrow = new DbUpdateException("saving failed", new InvalidOperationException("23503: violates foreign key"));
        RequestHandlerDelegate<BusinessResult> next = _ =>
        {
            Begun();
            throw commitThrow;
        };

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() =>
            Behavior().Handle(new EraseCommand(), next, CancellationToken.None));

        Assert.Same(commitThrow, thrown);
        _sink.Verify(s => s.RecordFailureAsync(
                "subject-1",
                "request-1",
                "jana.novakova@example.com",
                "InvalidOperationException: 23503: violates foreign key",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_Refusal_After_The_Walk_Began_Writes_The_Key_As_The_Note()
    {
        RequestHandlerDelegate<BusinessResult> next = _ =>
        {
            Begun();
            return Task.FromResult(BusinessResult.Failure(new Error("userId", BusinessErrorMessage.GdprDeletionBlockedByOrder)));
        };

        var result = await Behavior().Handle(new EraseCommand(), next, CancellationToken.None);

        Assert.True(result.IsFailure);
        _sink.Verify(s => s.RecordFailureAsync(
                "subject-1", "request-1", "jana.novakova@example.com",
                BusinessErrorMessage.GdprDeletionBlockedByOrder,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_Refusal_Before_Any_Walk_Began_Writes_Nothing()
    {
        var refusal = BusinessResult.Failure(new Error("userId", BusinessErrorMessage.GdprDeletionBlockedByOrder));

        var result = await Behavior().Handle(new EraseCommand(), Returns(refusal), CancellationToken.None);

        Assert.Same(refusal, result);
        _sink.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Throw_Before_Any_Walk_Began_Writes_Nothing_And_Still_Propagates()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Behavior().Handle(new EraseCommand(), Throws(new InvalidOperationException("validator blew up")), CancellationToken.None));

        _sink.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Success_Writes_Nothing_Even_Though_The_Walk_Began()
    {
        RequestHandlerDelegate<BusinessResult> next = _ =>
        {
            Begun();
            return Task.FromResult(BusinessResult.Success());
        };

        var result = await Behavior().Handle(new EraseCommand(), next, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _sink.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Sink_That_Throws_Is_Swallowed_And_The_Erasure_Error_Is_What_Propagates()
    {
        _sink.Setup(s => s.RecordFailureAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("sink down"));
        var commitThrow = new DbUpdateException("saving failed");
        RequestHandlerDelegate<BusinessResult> next = _ =>
        {
            Begun();
            throw commitThrow;
        };

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() =>
            Behavior().Handle(new EraseCommand(), next, CancellationToken.None));

        Assert.Same(commitThrow, thrown);
    }
}
