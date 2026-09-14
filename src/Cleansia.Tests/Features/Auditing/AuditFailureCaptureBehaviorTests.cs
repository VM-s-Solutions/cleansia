using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
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
///     writes a Success=false row out-of-band whose ErrorCode is the FIRST rule's key, not the
///     ValidationError sentinel — both arms, the admin one on the owner's default (ADR-0062 D1). A reject
///     that travelled as a thrown RequestValidationException (the response could not carry it) records
///     the same key, not the exception type, then rethrows.
///   • A commit-throw (an exception propagating from the OUTER UnitOfWork after the inner AuditLog returned
///     a success) writes a Success=false row out-of-band, then rethrows.
///   • The gate is AuditGate.Resolve, shared with the inner behavior: admin Commands (opt-out) and
///     customer-marked Commands (opt-in); a Query, an Employee or an unmarked non-admin Command produces no row.
///   • The shared IAuditContext latch prevents double-writing a failure the inner behavior already recorded.
///   • Best-effort: a sink that throws is swallowed and never changes the error returned to the admin.
/// </summary>
public sealed class AuditFailureCaptureBehaviorTests
{
    public sealed record AdminRefundOrderCommand(string OrderId) : IRequest<BusinessResult>;

    public sealed record GetPagedAuditsQuery : IRequest<BusinessResult>;

    [AuditAction("customer.order.create", Audience = AuditAudience.Customer, ResourceType = "Order", AllowsAnonymousActor = true)]
    public sealed record CustomerCreateOrderCommand(string OrderId) : IRequest<BusinessResult>;

    public sealed record AdminListSomethingCommand(string OrderId) : IRequest<PagedData<string>>;

    [AuditAction("customer.order.list", Audience = AuditAudience.Customer, ResourceType = "Order")]
    public sealed record CustomerListSomethingCommand(string OrderId) : IRequest<PagedData<string>>;

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
            new AuditEntryFactory(session, new TestRequestMetadataProvider(), new HostAudienceProvider(JwtAudiences.Admin)),
            NullLogger<AuditFailureCaptureBehavior<TRequest, BusinessResult>>.Instance);

    private AuditFailureCaptureBehavior<TRequest, PagedData<string>> PagedBehavior<TRequest>(
        IUserSessionProvider session,
        IAuditContext? auditContext = null)
        where TRequest : notnull =>
        new(session,
            auditContext ?? new AuditContext(),
            _sink.Object,
            new AuditEntryFactory(session, new TestRequestMetadataProvider(), new HostAudienceProvider(JwtAudiences.Admin)),
            NullLogger<AuditFailureCaptureBehavior<TRequest, PagedData<string>>>.Instance);

    private static RequestHandlerDelegate<BusinessResult> Returns(BusinessResult result) => _ => Task.FromResult(result);

    private static RequestHandlerDelegate<PagedData<string>> Throws(RequestValidationException exception) =>
        _ => Task.FromException<PagedData<string>>(exception);

    [Fact]
    public async Task A_Validation_Reject_Of_An_Admin_Command_Writes_A_Success_False_Row_OutOfBand()
    {
        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Administrator));
        var rejected = ValidationResult.WithErrors([new Error("OrderId", BusinessErrorMessage.Required)]);

        var result = await behavior.Handle(new AdminRefundOrderCommand("ORD-1"), Returns(rejected), CancellationToken.None);

        Assert.Same(rejected, result);
        _sink.Verify(s => s.RecordFailureAsync(It.Is<AdminActionAudit>(a =>
            !a.Success && a.ErrorCode == BusinessErrorMessage.Required && a.Action == "AdminRefundOrder"),
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
    public async Task A_Thrown_Validation_Reject_Of_An_Admin_Command_Writes_The_First_Rules_Key_Not_The_Exception_Type_Then_Rethrows()
    {
        var behavior = PagedBehavior<AdminListSomethingCommand>(Session(UserProfile.Administrator));
        var rejected = new RequestValidationException([
            new Error("Limit", BusinessErrorMessage.PageSizeExceeded),
            new Error("OrderId", BusinessErrorMessage.Required)
        ]);

        var thrown = await Assert.ThrowsAsync<RequestValidationException>(() =>
            behavior.Handle(new AdminListSomethingCommand("ORD-1"), Throws(rejected), CancellationToken.None));

        Assert.Same(rejected, thrown);
        _sink.Verify(s => s.RecordFailureAsync(It.Is<AdminActionAudit>(a =>
            !a.Success && a.ErrorCode == BusinessErrorMessage.PageSizeExceeded && a.Action == "AdminListSomething"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Thrown_Validation_Reject_Already_Claimed_On_The_Latch_Is_Not_Double_Written()
    {
        var sharedContext = new AuditContext();
        Assert.True(sharedContext.TryClaimFailureRecording());
        var behavior = PagedBehavior<AdminListSomethingCommand>(Session(UserProfile.Administrator), sharedContext);

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            behavior.Handle(new AdminListSomethingCommand("ORD-1"),
                Throws(new RequestValidationException([new Error("Limit", BusinessErrorMessage.PageSizeExceeded)])),
                CancellationToken.None));

        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
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

    // ── the customer arm (ADR-0062 D1) ────────────────────────────────────────

    [Fact]
    public async Task A_Validation_Reject_Of_A_Customer_Command_Writes_A_Customer_Row_With_The_First_Rules_Key()
    {
        var session = new TestUserSessionProvider("cust-1", "cust@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())]);
        var behavior = Behavior<CustomerCreateOrderCommand>(session);
        var rejected = ValidationResult.WithErrors(
        [
            new Error("TotalPrice", BusinessErrorMessage.TotalPriceNotMatch),
            new Error("CurrencyId", BusinessErrorMessage.Required)
        ]);

        var result = await behavior.Handle(new CustomerCreateOrderCommand("ORD-1"), Returns(rejected), CancellationToken.None);

        Assert.Same(rejected, result);
        _sink.Verify(s => s.RecordFailureAsync(It.Is<CustomerActionAudit>(a =>
            !a.Success && a.ErrorCode == BusinessErrorMessage.TotalPriceNotMatch && a.UserId == "cust-1" && a.Action == "customer.order.create"),
            It.IsAny<CancellationToken>()), Times.Once);
        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Thrown_Validation_Reject_Of_A_Customer_Command_Writes_A_Customer_Row_With_The_First_Rules_Key_Then_Rethrows()
    {
        var session = new TestUserSessionProvider("cust-1", "cust@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())]);
        var behavior = PagedBehavior<CustomerListSomethingCommand>(session);
        var rejected = new RequestValidationException([new Error("Limit", BusinessErrorMessage.PageSizeExceeded)]);

        var thrown = await Assert.ThrowsAsync<RequestValidationException>(() =>
            behavior.Handle(new CustomerListSomethingCommand("ORD-1"), Throws(rejected), CancellationToken.None));

        Assert.Same(rejected, thrown);
        _sink.Verify(s => s.RecordFailureAsync(It.Is<CustomerActionAudit>(a =>
            !a.Success && a.ErrorCode == BusinessErrorMessage.PageSizeExceeded && a.UserId == "cust-1" && a.Action == "customer.order.list"),
            It.IsAny<CancellationToken>()), Times.Once);
        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_Anonymous_Validation_Reject_Is_Recorded_Only_Where_The_Marker_Allows_A_Guest()
    {
        var anonymous = new TestUserSessionProvider([]);
        var rejected = ValidationResult.WithErrors([new Error("TotalPrice", BusinessErrorMessage.TotalPriceNotMatch)]);

        await Behavior<CustomerCreateOrderCommand>(anonymous)
            .Handle(new CustomerCreateOrderCommand("ORD-1"), Returns(rejected), CancellationToken.None);
        _sink.Verify(s => s.RecordFailureAsync(It.Is<CustomerActionAudit>(a => a.UserId == null && a.ClientAudience == JwtAudiences.Admin),
            It.IsAny<CancellationToken>()), Times.Once);

        await Behavior<AdminRefundOrderCommand>(anonymous)
            .Handle(new AdminRefundOrderCommand("ORD-1"), Returns(rejected), CancellationToken.None);
        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<CustomerActionAudit>(), It.IsAny<CancellationToken>()), Times.Once);
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
