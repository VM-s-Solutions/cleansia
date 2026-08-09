using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Q-BROWSE-01, answered (b): the browse gate is the last cleaner-facing surface that answered "may
/// this cleaner see this job" without asking <see cref="OrderAvailability"/>. It admitted on seat count
/// and the ADR-0036 hold alone, so it let a cleaner open the detail of a cancelled order, a finished one
/// whose crew was never filled, and a card order whose money has not landed — three states
/// <c>TakeOrder</c> refuses, on a screen whose only purpose is deciding whether to take.
///
/// <para>Both directions, because a gate that refuses everything passes a refusal-only suite: each case
/// below differs from the admitted control in exactly one column, and the assigned-cleaner and
/// administrator cases prove the term narrows the BROWSE branch and nothing else — it is reached only
/// after <c>CanAccessOrderAsync</c> has already said yes to everyone who is on the order.</para>
/// </summary>
public class OrderBrowseGateOfferabilityTests
{
    private const string CallerUserId = "user-browse-caller";
    private const string CallerEmployeeId = "employee-browse-caller";
    private const string OtherEmployeeId = "employee-browse-other";
    private const string OrderId = "order-browse-1";

    [Fact]
    public async Task An_Offerable_Order_With_A_Free_Seat_Is_Browsable()
    {
        var order = Offerable();

        Assert.True(await Gate().CanBrowseOrderAsync(order, CancellationToken.None));
    }

    /// <summary>
    /// The cash one-off is the whole reason offerability is not a status list: on it the take IS the
    /// confirmation, so <c>New</c> admits — and only for cash.
    /// </summary>
    [Fact]
    public async Task A_New_Cash_One_Off_Is_Browsable()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            OrderId, OrderStatus.New, paymentType: PaymentType.Cash, paymentStatus: PaymentStatus.Pending);

        Assert.True(await Gate().CanBrowseOrderAsync(order, CancellationToken.None));
    }

    [Fact]
    public async Task A_Cancelled_Order_With_A_Free_Seat_Is_Not_Browsable()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            OrderId, OrderStatus.Cancelled, paymentType: PaymentType.Card, paymentStatus: PaymentStatus.Paid);

        Assert.False(await Gate().CanBrowseOrderAsync(order, CancellationToken.None));
    }

    /// <summary>
    /// The exposure the owner's answer closes as a side effect: nothing fills or frees a seat on a
    /// terminal transition, so a two-seat job one cleaner finished alone kept an open seat forever and
    /// the gate kept admitting strangers to it, months later.
    /// </summary>
    [Fact]
    public async Task A_Finished_Job_With_A_Seat_Nobody_Will_Ever_Fill_Is_Not_Browsable()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            OrderId, OrderStatus.Completed, paymentType: PaymentType.Card, paymentStatus: PaymentStatus.Paid);

        Assert.False(await Gate().CanBrowseOrderAsync(order, CancellationToken.None));
    }

    /// <summary>
    /// The state the preferred-offer push used to deep-link into: a card order between booking and the
    /// Stripe webhook. <c>CleanupStalePendingOrders</c> cancels it an hour later, so it is not merely
    /// un-takeable, it may be about to stop existing.
    /// </summary>
    [Fact]
    public async Task A_Card_Order_Whose_Money_Has_Not_Landed_Is_Not_Browsable()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            OrderId, OrderStatus.New, paymentType: PaymentType.Card, paymentStatus: PaymentStatus.Pending);

        Assert.False(await Gate().CanBrowseOrderAsync(order, CancellationToken.None));
    }

    /// <summary>
    /// A recurring occurrence survives only via Paid — cash included — because
    /// <c>AutoCancelStaleRecurringOrders</c> keys on <c>PaymentStatus</c> with no payment-type term.
    /// </summary>
    [Fact]
    public async Task An_Unconfirmed_Recurring_Cash_Occurrence_Is_Not_Browsable()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            OrderId,
            OrderStatus.Confirmed,
            paymentType: PaymentType.Cash,
            paymentStatus: PaymentStatus.Pending,
            recurringTemplateId: "tmpl-weekly-browse");

        Assert.False(await Gate().CanBrowseOrderAsync(order, CancellationToken.None));
    }

    /// <summary>
    /// The half of the change that must NOT move: the cleaner who did the job keeps reading it after it
    /// is finished — through <c>CanAccessOrderAsync</c>, which the browse branch is only reached past.
    /// </summary>
    [Fact]
    public async Task The_Assigned_Cleaner_Still_Reads_A_Finished_Job()
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId,
            OrderStatus.Completed,
            assignedEmployeeId: CallerEmployeeId,
            paymentType: PaymentType.Card,
            paymentStatus: PaymentStatus.Paid);

        Assert.True(await Gate().CanBrowseOrderAsync(order, CancellationToken.None));
    }

    [Fact]
    public async Task An_Administrator_Still_Reads_A_Finished_Job()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            OrderId, OrderStatus.Completed, paymentType: PaymentType.Card, paymentStatus: PaymentStatus.Paid);

        Assert.True(await Administrator().CanBrowseOrderAsync(order, CancellationToken.None));
    }

    /// <summary>
    /// The other seat term, unchanged: an offerable order whose crew is complete is still refused, so
    /// the offerability conjunct did not replace the seat one.
    /// </summary>
    [Fact]
    public async Task An_Offerable_Order_With_No_Free_Seat_Is_Not_Browsable()
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId,
            OrderStatus.Confirmed,
            assignedEmployeeId: OtherEmployeeId,
            paymentType: PaymentType.Card,
            paymentStatus: PaymentStatus.Paid,
            maxEmployees: 1);

        Assert.False(await Gate().CanBrowseOrderAsync(order, CancellationToken.None));
    }

    private static Order Offerable() => ValidatorTestHelpers.BuildEmptyOrder(
        OrderId, OrderStatus.Confirmed, paymentType: PaymentType.Card, paymentStatus: PaymentStatus.Paid);

    private static OrderAccessService Gate() => Build(UserProfile.Employee, CallerEmployeeId);

    private static OrderAccessService Administrator() => Build(UserProfile.Administrator, null);

    private static OrderAccessService Build(UserProfile role, string? employeeId)
    {
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(CallerUserId);
        session.Setup(s => s.GetEmployeeId()).Returns(employeeId);
        session
            .Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, role.ToString()));

        return new OrderAccessService(session.Object, new Mock<IEmployeeRepository>().Object);
    }
}
