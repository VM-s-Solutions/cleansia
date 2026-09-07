using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// The preferred-cleaner gate on the EDIT path.
///
/// <para><c>CreateRecurringBooking</c> has always refused a cleaner the customer has never had a
/// completed order with (ADR-0036 D9) — the hold withholds every occurrence from the board, so
/// without the gate a customer could hand their schedule to any employee they can name. Update
/// carried no such rule and, until now, no <c>PreferredEmployeeId</c> at all: the field could be
/// set once at creation and never changed. Making it editable without the gate would have made the
/// create-time check a formality, since a template created with no preference could then be edited
/// into one.</para>
/// </summary>
public class UpdateRecurringBookingPreferredEmployeeTests
{
    private const string UserId = "user-1";
    private const string TemplateId = "tpl-1";
    private const string EmployeeId = "emp-1";

    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();

    public UpdateRecurringBookingPreferredEmployeeTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _templateRepository
            .Setup(r => r.ExistsAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _templateRepository
            .Setup(r => r.GetByIdAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTemplate());
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildActiveMembership());
    }

    private static RecurringBookingTemplate BuildTemplate()
    {
        var template = RecurringBookingTemplate.Create(
            userId: UserId,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: System.DayOfWeek.Thursday,
            timeOfDay: new TimeOnly(10, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: "addr-1",
            selectedServiceIds: ["svc-1"],
            selectedPackageIds: [],
            paymentType: PaymentType.Cash,
            startsOn: DateTime.UtcNow.AddDays(1));
        template.Id = TemplateId;
        return template;
    }

    private static UserMembership BuildActiveMembership()
    {
        var plan = MembershipPlan.Create(
            code: "PLUS",
            name: "Cleansia Plus",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_plus",
            discountPercentage: 10m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true);
        return UserMembership.Create(
            userId: UserId,
            membershipPlanId: plan.Id,
            stripeSubscriptionId: "sub_1",
            currentPeriodStart: DateTime.UtcNow.AddDays(-1),
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
    }

    private UpdateRecurringBooking.Validator CreateValidator() =>
        new(_templateRepository.Object, _membershipRepository.Object, _session.Object,
            _orderRepository.Object);

    private static UpdateRecurringBooking.Command CommandWith(string? preferredEmployeeId) =>
        new(
            TemplateId: TemplateId,
            Frequency: (int)RecurrenceFrequency.Weekly,
            DayOfWeek: (int)System.DayOfWeek.Thursday,
            TimeOfDay: "10:00",
            Rooms: 2,
            Bathrooms: 1,
            SavedAddressId: "addr-1",
            SelectedServiceIds: ["svc-1"],
            SelectedPackageIds: [],
            PaymentType: (int)PaymentType.Cash,
            StartsOn: DateTime.UtcNow.AddDays(3),
            EndsOn: null,
            PreferredEmployeeId: preferredEmployeeId);

    private void ArrangeServedByEmployee(bool served) =>
        _orderRepository
            .Setup(r => r.UserHasCompletedOrderWithEmployeeAsync(
                UserId, EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(served);

    [Fact]
    public async Task A_cleaner_who_has_finished_a_clean_for_this_customer_can_be_preferred()
    {
        ArrangeServedByEmployee(true);

        var result = await CreateValidator().ValidateAsync(CommandWith(EmployeeId));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// The rule that matters: naming a stranger is refused, so the edit path cannot be used to
    /// route a schedule to a cleaner the customer has no history with.
    /// </summary>
    [Fact]
    public async Task A_cleaner_the_customer_has_never_had_is_refused()
    {
        ArrangeServedByEmployee(false);

        var result = await CreateValidator().ValidateAsync(CommandWith(EmployeeId));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.ErrorMessage == BusinessErrorMessage.PreferredEmployeeNotEligible);
    }

    /// <summary>
    /// Clearing the preference is how a customer goes back to "anyone", so it must not be gated —
    /// and it must not cost a repository round trip either.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Clearing_the_preference_is_allowed_and_asks_nothing(string? cleared)
    {
        var result = await CreateValidator().ValidateAsync(CommandWith(cleared));

        Assert.True(result.IsValid);
        _orderRepository.Verify(
            r => r.UserHasCompletedOrderWithEmployeeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_name_a_preferred_cleaner()
    {
        _session.Setup(s => s.GetUserId()).Returns((string?)null);
        ArrangeServedByEmployee(true);

        var result = await CreateValidator().ValidateAsync(CommandWith(EmployeeId));

        Assert.False(result.IsValid);
    }
}
