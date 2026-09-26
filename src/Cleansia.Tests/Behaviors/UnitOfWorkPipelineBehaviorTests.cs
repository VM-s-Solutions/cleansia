using Cleansia.Config.Validation;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Behaviors;

/// <summary>
/// F11 / ADR-0002 D4 — the pipeline must NOT commit on a validation failure.
///
/// Two gates, written test-first (red → green) per <c>knowledge/testing.md</c>:
///   • <see cref="Validation_Behavior_Is_Registered_Before_UnitOfWork_Behavior"/> — ADR-0002
///     "How a reviewer verifies" check #4 (pipeline-order): the behaviors resolve from a built
///     <see cref="ServiceProvider"/> in the order <c>Validation → UnitOfWork → Handler</c> (outer →
///     inner). MediatR runs behaviors in registration order, so Validation must be registered first.
///   • <see cref="Failing_Command_Does_Not_Commit"/> — ADR-0002 check #10 (F11 regression): a
///     <c>*Command</c> whose inner <c>next()</c> returns a failure <see cref="BusinessResult"/> must
///     NOT trigger <see cref="IUnitOfWork.CommitAsync"/>. Paired with the happy-path
///     <see cref="Succeeding_Command_Commits_Exactly_Once"/> and the defense-in-depth /
///     guard cases.
/// </summary>
public class UnitOfWorkPipelineBehaviorTests
{
    // ── A *Command request and a non-Command request, both returning BusinessResult ──
    // The UoW behavior keys the commit on the request TYPE NAME ending in "Command"
    // (UnitOfWorkPipelineBehavior.IsNotCommand). These minimal stand-ins exercise that
    // guard without dragging in a real feature handler.
    public sealed record FakeCommand : IRequest<BusinessResult>;

    public sealed record FakeQuery : IRequest<BusinessResult>;

    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private UnitOfWorkPipelineBehavior<TRequest, BusinessResult> Behavior<TRequest>()
        where TRequest : notnull =>
        new(_unitOfWork.Object);

    // ── ADR-0002 verify #4 — pipeline order (outer → inner) ───────────────────

    [Fact]
    public void Validation_Behavior_Is_Registered_Before_UnitOfWork_Behavior()
    {
        // The order of the IPipelineBehavior<,> open-generic registrations IS the outer→inner
        // execution order MediatR resolves at runtime. We assert against the registration
        // descriptors (not activated instances) because the behaviors need a logger + validators to
        // construct — irrelevant to the ordering contract this gate protects.
        var services = new ServiceCollection().AddValidators();

        var behaviorTypes = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(d => d.ImplementationType)
            .ToList();

        var validationIndex = behaviorTypes.IndexOf(typeof(ValidationPipelineBehavior<,>));
        var unitOfWorkIndex = behaviorTypes.IndexOf(typeof(UnitOfWorkPipelineBehavior<,>));

        Assert.True(validationIndex >= 0, "ValidationPipelineBehavior must be registered.");
        Assert.True(unitOfWorkIndex >= 0, "UnitOfWorkPipelineBehavior must be registered.");
        Assert.True(
            validationIndex < unitOfWorkIndex,
            "ADR-0002 D4: ValidationPipelineBehavior must be registered BEFORE UnitOfWorkPipelineBehavior " +
            "so validation is the OUTER behavior (Validation → UnitOfWork → Handler) and a rejected " +
            "command never reaches the commit.");
    }

    // ── ADR-0002 verify #10 — F11 regression: no commit on failure ──────

    [Fact]
    public async Task Failing_Command_Does_Not_Commit()
    {
        // Simulate the post-reorder shape directly at the UoW behavior: it sees a failure
        // BusinessResult coming back from the inner pipeline (validation rejected the command) and
        // MUST NOT commit. The caller still receives the failure result unchanged.
        var failure = BusinessResult.Failure(new Error("test.rejected", "rejected by validator"));
        var behavior = Behavior<FakeCommand>();

        var result = await behavior.Handle(
            new FakeCommand(),
            (_) => Task.FromResult(failure),
            CancellationToken.None);

        Assert.Same(failure, result);
        Assert.True(result.IsFailure);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
    }

    // ── happy path: a succeeding command commits exactly once, after the handler ─

    [Fact]
    public async Task Succeeding_Command_Commits_Exactly_Once()
    {
        var success = BusinessResult.Success();
        var behavior = Behavior<FakeCommand>();

        var result = await behavior.Handle(
            new FakeCommand(),
            (_) => Task.FromResult(success),
            CancellationToken.None);

        Assert.Same(success, result);
        Assert.True(result.IsSuccess);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.Rollback(), Times.Never);
    }

    // ── defense-in-depth & the IsNotCommand guard ────────────────────────────────

    [Fact]
    public async Task Non_Command_Request_Never_Commits()
    {
        // The IsNotCommand short-circuit: a request whose type name does not end in "Command"
        // returns next() without ever touching the unit of work — even on success.
        var success = BusinessResult.Success();
        var behavior = Behavior<FakeQuery>();

        var result = await behavior.Handle(
            new FakeQuery(),
            (_) => Task.FromResult(success),
            CancellationToken.None);

        Assert.Same(success, result);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.Rollback(), Times.Never);
    }

    [Fact]
    public async Task Failed_Command_ReleasesItsLocksBeforeTheOuterErasureFailureRecorder()
    {
        var events = new List<string>();
        _unitOfWork.Setup(u => u.Rollback()).Callback(() => events.Add("rollback"));
        var failure = BusinessResult.Failure(new Error("userId", "deletion.refused"));
        var command = new FakeCommand();

        var result = await OuterFailureRecorder(events).Handle(command,
            ct => Behavior<FakeCommand>().Handle(command, _ => Task.FromResult(failure), ct),
            CancellationToken.None);

        Assert.Same(failure, result);
        Assert.Equal(new[] { "rollback", "failure-recorded" }, events);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Throwing_Command_ReleasesItsLocksBeforeTheOuterErasureFailureRecorder(bool commitThrows)
    {
        var events = new List<string>();
        var failure = new InvalidOperationException(commitThrows ? "commit failed" : "handler failed");
        _unitOfWork.Setup(u => u.Rollback()).Callback(() => events.Add("rollback"));
        if (commitThrows)
            _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        RequestHandlerDelegate<BusinessResult> next = _ => commitThrows
            ? Task.FromResult(BusinessResult.Success())
            : Task.FromException<BusinessResult>(failure);
        var command = new FakeCommand();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OuterFailureRecorder(events).Handle(command,
                ct => Behavior<FakeCommand>().Handle(command, next, ct), CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Equal(new[] { "rollback", "failure-recorded" }, events);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), commitThrows ? Times.Once() : Times.Never());
    }

    private static ErasureFailureCaptureBehavior<FakeCommand, BusinessResult> OuterFailureRecorder(List<string> events)
    {
        var attempt = new ErasureAttempt();
        attempt.Begin("subject-1", "request-1", "self");
        var sink = new Mock<IGdprDeletionFailureSink>();
        sink.Setup(s => s.RecordFailureAsync("subject-1", "request-1", "self",
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => events.Add("failure-recorded"))
            .Returns(Task.CompletedTask);
        return new(attempt, sink.Object,
            NullLogger<ErasureFailureCaptureBehavior<FakeCommand, BusinessResult>>.Instance);
    }
}
