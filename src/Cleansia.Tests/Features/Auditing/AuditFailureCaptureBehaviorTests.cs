using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0012 D2.1/D2.2 — the OUTERMOST AuditFailureCaptureBehavior: the backstop for the two failed-admin-
/// action shapes the inner AuditLogBehavior structurally cannot see.
///   • A validation reject (a short-circuited BusinessResult failure that never reached UnitOfWork/AuditLog)
///     writes a Success=false row out-of-band.
///   • A commit-throw (an exception propagating from the OUTER UnitOfWork after the inner AuditLog returned
///     a success) writes a Success=false row out-of-band, then rethrows.
///   • The gate is identical to the inner behavior (admin Command only); a query / non-admin produces no row.
///   • The shared IAuditContext latch prevents double-writing a failure the inner behavior already recorded.
///   • Best-effort: a sink that throws is swallowed and never changes the error returned to the admin.
/// </summary>
public sealed class AuditFailureCaptureBehaviorTests
{
    public sealed record AdminRefundOrderCommand(string OrderId) : IRequest<BusinessResult>;

    public sealed record GetPagedAuditsQuery : IRequest<BusinessResult>;

    private readonly Mock<IAuditFailureSink> _sink = new();

    private static IUserSessionProvider Session(UserProfile? role) =>
        new TestUserSessionProvider(
            "admin-1",
            "admin@cleansia.test",
            role is null ? null : [new Claim(ClaimTypes.Role, role.Value.ToString())]);

    private AuditFailureCaptureBehavior<TRequest, BusinessResult> Behavior<TRequest>(
        IUserSessionProvider session,
        IAuditContext? auditContext = null)
        where TRequest : notnull =>
        new(session,
            auditContext ?? new AuditContext(),
            _sink.Object,
            new AuditEntryFactory(session),
            NullLogger<AuditFailureCaptureBehavior<TRequest, BusinessResult>>.Instance);

    private static RequestHandlerDelegate<BusinessResult> Returns(BusinessResult result) => _ => Task.FromResult(result);

    [Fact]
    public async Task A_Validation_Reject_Of_An_Admin_Command_Writes_A_Success_False_Row_OutOfBand()
    {
        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Administrator));
        var rejected = BusinessResult.Failure(new Error("validation.required", "UserId is required"));

        var result = await behavior.Handle(new AdminRefundOrderCommand("ORD-1"), Returns(rejected), CancellationToken.None);

        Assert.Same(rejected, result);
        _sink.Verify(s => s.RecordFailureAsync(It.Is<AdminActionAudit>(a =>
            !a.Success && a.ErrorCode == "validation.required" && a.Action == "AdminRefundOrder"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_CommitThrow_On_An_Admin_Command_Writes_A_Success_False_Row_OutOfBand_Then_Rethrows()
    {
        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Administrator));
        var commitFailure = new InvalidOperationException("SaveChangesAsync failed");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(new AdminRefundOrderCommand("ORD-1"),
                _ => Task.FromException<BusinessResult>(commitFailure), CancellationToken.None));

        Assert.Same(commitFailure, thrown);
        _sink.Verify(s => s.RecordFailureAsync(It.Is<AdminActionAudit>(a =>
            !a.Success && a.ErrorCode == nameof(InvalidOperationException) && a.Action == "AdminRefundOrder"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_NonAdmin_Validation_Reject_Produces_No_Row()
    {
        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Customer));

        await behavior.Handle(new AdminRefundOrderCommand("ORD-1"),
            Returns(BusinessResult.Failure(new Error("validation.required", "nope"))), CancellationToken.None);

        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_Admin_Query_Failure_Is_Never_Audited()
    {
        var behavior = Behavior<GetPagedAuditsQuery>(Session(UserProfile.Administrator));

        await behavior.Handle(new GetPagedAuditsQuery(),
            Returns(BusinessResult.Failure(new Error("x", "y"))), CancellationToken.None);

        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Successful_Admin_Command_Writes_No_Failure_Row()
    {
        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Administrator));

        await behavior.Handle(new AdminRefundOrderCommand("ORD-1"), Returns(BusinessResult.Success()), CancellationToken.None);

        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Failure_Already_Claimed_By_The_Inner_Behavior_Is_Not_Double_Written()
    {
        var sharedContext = new AuditContext();
        // The inner behavior claimed the latch first (a handler-returned business failure it recorded).
        Assert.True(sharedContext.TryClaimFailureRecording());

        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Administrator), sharedContext);

        await behavior.Handle(new AdminRefundOrderCommand("ORD-1"),
            Returns(BusinessResult.Failure(new Error("refund.too_large", "exceeds total"))), CancellationToken.None);

        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Sink_That_Throws_On_A_Validation_Reject_Is_Swallowed_So_The_Result_Is_Unchanged()
    {
        _sink.Setup(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("sink down"));
        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Administrator));
        var rejected = BusinessResult.Failure(new Error("validation.required", "nope"));

        var result = await behavior.Handle(new AdminRefundOrderCommand("ORD-1"), Returns(rejected), CancellationToken.None);

        Assert.Same(rejected, result);
    }

    [Fact]
    public async Task A_Sink_That_Throws_On_A_CommitThrow_Is_Swallowed_So_The_Original_Exception_Is_Unchanged()
    {
        _sink.Setup(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("sink down"));
        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Administrator));
        var commitFailure = new InvalidOperationException("SaveChangesAsync failed");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(new AdminRefundOrderCommand("ORD-1"),
                _ => Task.FromException<BusinessResult>(commitFailure), CancellationToken.None));

        Assert.Same(commitFailure, thrown);
    }
}
