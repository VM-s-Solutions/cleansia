using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Common;
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
///     customer table; an Employee lands nowhere; an Administrator running it lands in the admin table;
///     an anonymous caller of a guest-allowed marker lands in the customer table on a customer host and
///     nowhere on any other; a handler that declines the success row leaves none, and its refusal is
///     still recorded.
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

    private AuditLogBehavior<TRequest, BusinessResult> Behavior<TRequest>(
        IUserSessionProvider session,
        string host = JwtAudiences.Admin,
        IAuditContext? auditContext = null)
        where TRequest : notnull =>
        new(session,
            new HostAudienceProvider(host),
            auditContext ?? new AuditContext(),
            _writer.Object,
            _sink.Object,
            new AuditEntryFactory(session, new TestRequestMetadataProvider(), new HostAudienceProvider(host)),
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
            session, new HostAudienceProvider(JwtAudiences.Admin), new AuditContext(), _writer.Object, _sink.Object,
            new AuditEntryFactory(session, new TestRequestMetadataProvider(), new HostAudienceProvider(JwtAudiences.Admin)),
            NullLogger<AuditLogBehavior<AdminCreateThingCommand, BusinessResult<string>>>.Instance);

        await behavior.Handle(new AdminCreateThingCommand(),
            _ => Task.FromResult(BusinessResult.Success("new-id")), CancellationToken.None);

        _writer.Verify(w => w.Add(It.Is<AdminActionAudit>(a => a.Success && a.Action == "AdminCreateThing")), Times.Once);
    }

    // ── the customer arm (ADR-0062 D1) ────────────────────────────────────────

    private static IUserSessionProvider CustomerSession() =>
        new TestUserSessionProvider("cust-1", "cust@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())]);

    [AuditAction("customer.session.login", Audience = AuditAudience.Customer, ResourceType = "User", AllowsAnonymousActor = true)]
    public sealed record SignInOrRegisterCommand(string Email) : IRequest<BusinessResult>;

    private static IUserSessionProvider AnonymousSession() => new TestUserSessionProvider([]);

    [Fact]
    public async Task A_Customer_Command_On_Success_Adds_One_Customer_Row_And_No_Admin_Row()
    {
        var behavior = Behavior<CustomerCancelOrderCommand>(CustomerSession(), JwtAudiences.Customer);

        await behavior.Handle(new CustomerCancelOrderCommand("ORD-1"), Returns(BusinessResult.Success()), CancellationToken.None);

        _writer.Verify(w => w.Add(It.Is<CustomerActionAudit>(a =>
            a.Success && a.Action == "customer.order.cancel" && a.UserId == "cust-1" && a.ResourceId == "ORD-1")), Times.Once);
        _writer.Verify(w => w.Add(It.IsAny<AdminActionAudit>()), Times.Never);
        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<CustomerActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Customer_Business_Failure_Writes_A_Customer_Row_With_The_Key_OutOfBand()
    {
        var behavior = Behavior<CustomerCancelOrderCommand>(CustomerSession(), JwtAudiences.Customer);
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
        var behavior = Behavior<CustomerCancelOrderCommand>(CustomerSession(), JwtAudiences.Customer);
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
        var behavior = Behavior<CustomerCancelOrderCommand>(Session(UserProfile.Employee), JwtAudiences.Partner);

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
        var behavior = Behavior<AdminRefundOrderCommand>(CustomerSession(), JwtAudiences.Customer);

        await behavior.Handle(new AdminRefundOrderCommand("ORD-1"), Returns(BusinessResult.Success()), CancellationToken.None);

        _writer.Verify(w => w.Add(It.IsAny<CustomerActionAudit>()), Times.Never);
        _writer.Verify(w => w.Add(It.IsAny<AdminActionAudit>()), Times.Never);
    }

    // ── the host gate on the anonymous arm ────────────────────────────────────

    [Fact]
    public async Task An_Anonymous_Success_On_A_Customer_Host_Adds_A_Customer_Row_With_The_Actor_The_Handler_Named()
    {
        var auditContext = new AuditContext();
        auditContext.RecordEvidence("User", "user-9", new { method = "Password" }, actorUserId: "user-9");
        var behavior = Behavior<SignInOrRegisterCommand>(AnonymousSession(), JwtAudiences.Customer, auditContext);

        await behavior.Handle(new SignInOrRegisterCommand("x@cleansia.test"), Returns(BusinessResult.Success()), CancellationToken.None);

        _writer.Verify(w => w.Add(It.Is<CustomerActionAudit>(a =>
            a.Success && a.Action == "customer.session.login" && a.UserId == "user-9" && a.ResourceId == "user-9"
            && a.ClientAudience == JwtAudiences.Customer)), Times.Once);
    }

    [Theory]
    [InlineData(JwtAudiences.Partner)]
    [InlineData(JwtAudiences.Mobile)]
    [InlineData(JwtAudiences.Admin)]
    public async Task An_Anonymous_Act_Off_The_Customer_Hosts_Lands_Nowhere_Whether_It_Succeeds_Or_Is_Refused(string host)
    {
        var behavior = Behavior<SignInOrRegisterCommand>(AnonymousSession(), host);

        await behavior.Handle(new SignInOrRegisterCommand("x@cleansia.test"), Returns(BusinessResult.Success()), CancellationToken.None);
        await behavior.Handle(new SignInOrRegisterCommand("x@cleansia.test"),
            Returns(BusinessResult.Failure(new Error("Email", BusinessErrorMessage.InvalidPassword))), CancellationToken.None);

        _writer.Verify(w => w.Add(It.IsAny<CustomerActionAudit>()), Times.Never);
        _writer.Verify(w => w.Add(It.IsAny<AdminActionAudit>()), Times.Never);
        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<CustomerActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A refused anonymous act names nobody: the row is the key, the IP and the device, never the address the command carried.</summary>
    [Fact]
    public async Task An_Anonymous_Refusal_On_A_Customer_Host_Writes_A_Row_With_No_User_No_Payload_And_No_Resource()
    {
        var behavior = Behavior<SignInOrRegisterCommand>(AnonymousSession(), JwtAudiences.Customer);

        await behavior.Handle(new SignInOrRegisterCommand("unknown@cleansia.test"),
            Returns(BusinessResult.Failure(new Error("Email", BusinessErrorMessage.NotExistingUserWithEmail))), CancellationToken.None);

        _sink.Verify(s => s.RecordFailureAsync(It.Is<CustomerActionAudit>(a =>
            !a.Success && a.ErrorCode == BusinessErrorMessage.NotExistingUserWithEmail
            && a.UserId == null && a.PayloadJson == null && a.ResourceId == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── a declined success row ─────────────────────────────────────────────────

    [Fact]
    public async Task A_Handler_That_Declines_The_Success_Row_Leaves_No_Row()
    {
        var auditContext = new AuditContext();
        var behavior = Behavior<SignInOrRegisterCommand>(AnonymousSession(), JwtAudiences.Customer, auditContext);

        await behavior.Handle(new SignInOrRegisterCommand("new@cleansia.test"), _ =>
        {
            auditContext.DeclineSuccessRow();
            return Task.FromResult(BusinessResult.Success());
        }, CancellationToken.None);

        _writer.Verify(w => w.Add(It.IsAny<CustomerActionAudit>()), Times.Never);
        _sink.Verify(s => s.RecordFailureAsync(It.IsAny<CustomerActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Refusal_After_The_Success_Row_Was_Declined_Is_Still_Recorded()
    {
        var auditContext = new AuditContext();
        var behavior = Behavior<SignInOrRegisterCommand>(AnonymousSession(), JwtAudiences.Customer, auditContext);

        await behavior.Handle(new SignInOrRegisterCommand("new@cleansia.test"), _ =>
        {
            auditContext.DeclineSuccessRow();
            return Task.FromResult(BusinessResult.Failure(new Error("Email", BusinessErrorMessage.ExistingUserWithEmail)));
        }, CancellationToken.None);

        _sink.Verify(s => s.RecordFailureAsync(It.Is<CustomerActionAudit>(a =>
            !a.Success && a.ErrorCode == BusinessErrorMessage.ExistingUserWithEmail), It.IsAny<CancellationToken>()), Times.Once);
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
