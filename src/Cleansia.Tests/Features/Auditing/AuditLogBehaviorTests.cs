using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Behaviors;
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
/// ADR-0012 D2/D2.1/D2.2/D3 — the AuditLogBehavior gate + write paths, unit level.
///   • TC-AUDIT-GATE: the gate is AuditGate.Resolve, shared with the outer behavior: admin Commands (opt-out)
///     and customer-marked Commands (opt-in); a Query, an Employee or an unmarked non-admin Command produces no row.
///   • Success: the row is added to the scoped DbContext (via IAuditWriter) — atomic with the action.
///   • TC-AUDIT-FAILURE (business): a failure result writes a Success=false row out-of-band (the sink),
///     NOT to the scoped writer (the UoW won't commit it). The row's ErrorCode is the refusal KEY
///     (Error.Message), not the field name in Error.Code — both arms, the admin one on the owner's default (ADR-0062 D1).
///   • The customer arm (ADR-0062 D1): a Customer running a customer-marked Command lands in the
///     customer table; an Employee lands nowhere; an Administrator running it lands in the admin table.
///   • TC-AUDIT-FAILURE (exception): the failure row is written out-of-band then the exception rethrows;
///     a sink that throws is swallowed — the ORIGINAL error reaches the caller unchanged.
/// </summary>
public sealed class AuditLogBehaviorTests
{
    public sealed record AdminRefundOrderCommand(string OrderId) : IRequest<BusinessResult>;

    public sealed record GetPagedAuditsQuery : IRequest<BusinessResult>;

    [AuditAction("customer.order.cancel", Audience = AuditAudience.Customer, ResourceType = "Order")]
    public sealed record CustomerCancelOrderCommand(string OrderId) : IRequest<BusinessResult>;

    private readonly Mock<IAuditWriter> _writer = new();
    private readonly Mock<IAuditFailureSink> _sink = new();

    private static IUserSessionProvider Session(UserProfile? role) =>
        new TestUserSessionProvider(
            "admin-1",
            "admin@cleansia.test",
            role is null ? null : [new Claim(ClaimTypes.Role, role.Value.ToString())]);

    private AuditLogBehavior<TRequest, BusinessResult> Behavior<TRequest>(IUserSessionProvider session)
        where TRequest : notnull =>
        new(session,
            new AuditContext(),
            _writer.Object,
            _sink.Object,
            new AuditEntryFactory(session, new TestRequestMetadataProvider(), new HostAudienceProvider(JwtAudiences.Admin)),
            NullLogger<AuditLogBehavior<TRequest, BusinessResult>>.Instance);

    private static RequestHandlerDelegate<BusinessResult> Returns(BusinessResult result) => _ => Task.FromResult(result);

    // ── TC-AUDIT-GATE ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Admin_Command_On_Success_Adds_Exactly_One_Row_Via_The_Scoped_Writer()
    {
        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Administrator));

        await behavior.Handle(new AdminRefundOrderCommand("ORD-1"), Returns(BusinessResult.Success()), CancellationToken.None);

        _writer.Verify(w => w.Add(It.Is<AdminActionAudit>(a =>
            a.Success && a.Action == "AdminRefundOrder" && a.ActorId == "admin-1" && a.ResourceId == "ORD-1")), Times.Once);
        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_NonAdmin_Caller_Mutation_Produces_No_Row()
    {
        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Customer));

        await behavior.Handle(new AdminRefundOrderCommand("ORD-1"), Returns(BusinessResult.Success()), CancellationToken.None);

        _writer.Verify(w => w.Add(It.IsAny<AdminActionAudit>()), Times.Never);
        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Caller_With_No_Role_Claim_Produces_No_Row()
    {
        var behavior = Behavior<AdminRefundOrderCommand>(Session(role: null));

        await behavior.Handle(new AdminRefundOrderCommand("ORD-1"), Returns(BusinessResult.Success()), CancellationToken.None);

        _writer.Verify(w => w.Add(It.IsAny<AdminActionAudit>()), Times.Never);
    }

    [Fact]
    public async Task An_Admin_Query_Is_Never_Audited()
    {
        var behavior = Behavior<GetPagedAuditsQuery>(Session(UserProfile.Administrator));

        await behavior.Handle(new GetPagedAuditsQuery(), Returns(BusinessResult.Success()), CancellationToken.None);

        _writer.Verify(w => w.Add(It.IsAny<AdminActionAudit>()), Times.Never);
        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── TC-AUDIT-FAILURE ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_Business_Failure_Writes_A_Success_False_Row_OutOfBand_Not_To_The_Scoped_Writer()
    {
        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Administrator));
        var failure = BusinessResult.Failure(new Error("Amount", "refund.too_large"));

        var result = await behavior.Handle(new AdminRefundOrderCommand("ORD-1"), Returns(failure), CancellationToken.None);

        Assert.Same(failure, result);
        _writer.Verify(w => w.Add(It.IsAny<AdminActionAudit>()), Times.Never);
        _sink.Verify(s => s.RecordFailureAsync(It.Is<AdminActionAudit>(a =>
            !a.Success && a.ErrorCode == "refund.too_large" && a.Action == "AdminRefundOrder"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Thrown_Handler_Writes_A_Failure_Row_OutOfBand_Then_Rethrows_The_Original_Exception()
    {
        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Administrator));
        var boom = new InvalidOperationException("db down");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(new AdminRefundOrderCommand("ORD-1"),
                _ => Task.FromException<BusinessResult>(boom), CancellationToken.None));

        Assert.Same(boom, thrown);
        _writer.Verify(w => w.Add(It.IsAny<AdminActionAudit>()), Times.Never);
        _sink.Verify(s => s.RecordFailureAsync(It.Is<AdminActionAudit>(a =>
            !a.Success && a.ErrorCode == nameof(InvalidOperationException)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Sink_That_Throws_Is_Swallowed_So_The_Original_Caller_Error_Is_Unchanged()
    {
        _sink.Setup(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("sink down"));
        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Administrator));
        var boom = new InvalidOperationException("db down");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(new AdminRefundOrderCommand("ORD-1"),
                _ => Task.FromException<BusinessResult>(boom), CancellationToken.None));

        Assert.Same(boom, thrown);
    }

    [Fact]
    public async Task A_Sink_That_Throws_On_A_Business_Failure_Is_Swallowed_So_The_Failure_Result_Is_Unchanged()
    {
        _sink.Setup(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("sink down"));
        var behavior = Behavior<AdminRefundOrderCommand>(Session(UserProfile.Administrator));
        var failure = BusinessResult.Failure(new Error("Amount", "refund.too_large"));

        var result = await behavior.Handle(new AdminRefundOrderCommand("ORD-1"), Returns(failure), CancellationToken.None);

        Assert.Same(failure, result);
    }

    // ── ICommand<T> generic-response path (BusinessResult<T> : BusinessResult) ──

    public sealed record AdminCreateThingCommand : IRequest<BusinessResult<string>>;

    [Fact]
    public async Task An_Admin_Command_Returning_A_Generic_BusinessResult_Success_Is_Audited()
    {
        var session = Session(UserProfile.Administrator);
        var behavior = new AuditLogBehavior<AdminCreateThingCommand, BusinessResult<string>>(
            session, new AuditContext(), _writer.Object, _sink.Object,
            new AuditEntryFactory(session, new TestRequestMetadataProvider(), new HostAudienceProvider(JwtAudiences.Admin)),
            NullLogger<AuditLogBehavior<AdminCreateThingCommand, BusinessResult<string>>>.Instance);

        await behavior.Handle(new AdminCreateThingCommand(),
            _ => Task.FromResult(BusinessResult.Success("new-id")), CancellationToken.None);

        _writer.Verify(w => w.Add(It.Is<AdminActionAudit>(a => a.Success && a.Action == "AdminCreateThing")), Times.Once);
    }

    // ── the customer arm (ADR-0062 D1) ────────────────────────────────────────

    private static IUserSessionProvider CustomerSession() =>
        new TestUserSessionProvider("cust-1", "cust@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())]);

    [Fact]
    public async Task A_Customer_Command_On_Success_Adds_One_Customer_Row_And_No_Admin_Row()
    {
        var behavior = Behavior<CustomerCancelOrderCommand>(CustomerSession());

        await behavior.Handle(new CustomerCancelOrderCommand("ORD-1"), Returns(BusinessResult.Success()), CancellationToken.None);

        _writer.Verify(w => w.Add(It.Is<CustomerActionAudit>(a =>
            a.Success && a.Action == "customer.order.cancel" && a.UserId == "cust-1" && a.ResourceId == "ORD-1")), Times.Once);
        _writer.Verify(w => w.Add(It.IsAny<AdminActionAudit>()), Times.Never);
        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<CustomerActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Customer_Business_Failure_Writes_A_Customer_Row_With_The_Key_OutOfBand()
    {
        var behavior = Behavior<CustomerCancelOrderCommand>(CustomerSession());
        var failure = BusinessResult.Failure(new Error("OrderId", "order.in_progress_cannot_cancel"));

        var result = await behavior.Handle(new CustomerCancelOrderCommand("ORD-1"), Returns(failure), CancellationToken.None);

        Assert.Same(failure, result);
        _writer.Verify(w => w.Add(It.IsAny<CustomerActionAudit>()), Times.Never);
        _sink.Verify(s => s.RecordFailureAsync(It.Is<CustomerActionAudit>(a =>
            !a.Success && a.ErrorCode == "order.in_progress_cannot_cancel" && a.ResourceId == "ORD-1"), It.IsAny<CancellationToken>()), Times.Once);
        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Thrown_Customer_Handler_Writes_A_Customer_Failure_Row_Then_Rethrows()
    {
        var behavior = Behavior<CustomerCancelOrderCommand>(CustomerSession());
        var boom = new InvalidOperationException("stripe down");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(new CustomerCancelOrderCommand("ORD-1"),
                _ => Task.FromException<BusinessResult>(boom), CancellationToken.None));

        Assert.Same(boom, thrown);
        _sink.Verify(s => s.RecordFailureAsync(It.Is<CustomerActionAudit>(a =>
            !a.Success && a.ErrorCode == nameof(InvalidOperationException)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_Employee_Running_A_Customer_Marked_Command_Produces_No_Row_Anywhere()
    {
        var behavior = Behavior<CustomerCancelOrderCommand>(Session(UserProfile.Employee));

        await behavior.Handle(new CustomerCancelOrderCommand("ORD-1"), Returns(BusinessResult.Success()), CancellationToken.None);

        _writer.Verify(w => w.Add(It.IsAny<CustomerActionAudit>()), Times.Never);
        _writer.Verify(w => w.Add(It.IsAny<AdminActionAudit>()), Times.Never);
    }

    [Fact]
    public async Task An_Administrator_Running_A_Customer_Marked_Command_Lands_In_The_Admin_Table()
    {
        var behavior = Behavior<CustomerCancelOrderCommand>(Session(UserProfile.Administrator));

        await behavior.Handle(new CustomerCancelOrderCommand("ORD-1"), Returns(BusinessResult.Success()), CancellationToken.None);

        _writer.Verify(w => w.Add(It.Is<AdminActionAudit>(a => a.Action == "customer.order.cancel" && a.ActorId == "admin-1")), Times.Once);
        _writer.Verify(w => w.Add(It.IsAny<CustomerActionAudit>()), Times.Never);
    }

    [Fact]
    public async Task A_Customer_Running_An_Unmarked_Command_Produces_No_Row()
    {
        var behavior = Behavior<AdminRefundOrderCommand>(CustomerSession());

        await behavior.Handle(new AdminRefundOrderCommand("ORD-1"), Returns(BusinessResult.Success()), CancellationToken.None);

        _writer.Verify(w => w.Add(It.IsAny<CustomerActionAudit>()), Times.Never);
        _writer.Verify(w => w.Add(It.IsAny<AdminActionAudit>()), Times.Never);
    }

    // ── opt-out marker ────────────────────────────────────────────────────────

    [AuditAction(Audited = false)]
    public sealed record OptedOutCommand : IRequest<BusinessResult>;

    [Fact]
    public async Task An_AuditAction_OptedOut_Command_Is_Not_Audited_Even_For_An_Admin()
    {
        var behavior = Behavior<OptedOutCommand>(Session(UserProfile.Administrator));

        await behavior.Handle(new OptedOutCommand(), Returns(BusinessResult.Success()), CancellationToken.None);

        _writer.Verify(w => w.Add(It.IsAny<AdminActionAudit>()), Times.Never);
    }
}
