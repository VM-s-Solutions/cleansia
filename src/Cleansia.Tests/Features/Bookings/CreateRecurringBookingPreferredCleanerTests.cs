using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// ADR-0036 D8 — a recurring template may name a preferred cleaner, and the eligibility that governs it
/// is the SAME rule the one-off path enforces (<c>CreateOrder</c>'s
/// <c>UserHasCompletedOrderWithEmployeeAsync</c>): without it <c>PreferredEmployeeId</c> is any employee
/// id a client cares to send, and the hold would let a customer withhold every occurrence from the whole
/// board and hand it to a cleaner they name (D9.1).
///
/// <para>It is checked ONCE, here, and never re-checked by the sweep. The relationship is monotone — a
/// Completed order does not un-complete — and this is the one gate in the set that needs the caller's
/// identity, which a 03:00 sweep does not have. Everything that CAN lapse (membership, the cleaner
/// leaving, the mute, reachability, a clash at the occurrence's own instant) is re-run per occurrence by
/// <c>IPreferredCleanerHoldResolver</c>, where a "no" costs the hold and the push and never the cleaning.
/// Reject where a human can react; degrade where nobody can.</para>
/// </summary>
public class CreateRecurringBookingPreferredCleanerTests
{
    private const string UserId = "user-plus-pref";
    private const string SavedAddressId = "saved-address-pref";
    private const string ServedCleanerId = "employee-served-1";
    private const string StrangerCleanerId = "employee-stranger-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();

    public CreateRecurringBookingPreferredCleanerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _orderRepository
            .Setup(r => r.UserHasCompletedOrderWithEmployeeAsync(
                UserId, ServedCleanerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderRepository
            .Setup(r => r.UserHasCompletedOrderWithEmployeeAsync(
                UserId, StrangerCleanerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _savedAddressRepository
            .Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ArrangeSavedAddress()]);
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArrangeActiveMembership());
    }

    // ── The gate ──

    [Fact]
    public async Task A_Cleaner_Who_Never_Served_This_Customer_Is_Refused()
    {
        var result = await CreateValidator().ValidateAsync(CommandWith(StrangerCleanerId));

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.PreferredEmployeeNotEligible, failure.ErrorMessage);
        Assert.Equal(nameof(CreateRecurringBooking.Command.PreferredEmployeeId), failure.PropertyName);
    }

    [Fact]
    public async Task A_Cleaner_Who_Completed_An_Order_For_This_Customer_Passes()
    {
        var result = await CreateValidator().ValidateAsync(CommandWith(ServedCleanerId));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task No_Preference_Never_Asks_The_Eligibility_Question()
    {
        var result = await CreateValidator().ValidateAsync(CommandWith(null));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
        _orderRepository.Verify(
            r => r.UserHasCompletedOrderWithEmployeeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// The eligibility is "has THIS customer been served by them", so with no caller there is no
    /// question to answer and the only safe answer is no. <c>CreateOrder</c>'s <c>When</c> skips the
    /// rule for a session-less caller because that route is genuinely anonymous; this one is not.
    /// </summary>
    [Fact]
    public async Task A_Preference_With_No_Session_Is_Refused_Rather_Than_Skipped()
    {
        _session.Setup(s => s.GetUserId()).Returns((string?)null);

        var result = await CreateValidator().ValidateAsync(CommandWith(ServedCleanerId));

        Assert.False(result.IsValid);
        Assert.Equal(
            BusinessErrorMessage.PreferredEmployeeNotEligible,
            Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task The_Eligibility_Question_Is_Scoped_To_The_Session_User_Not_The_Command()
    {
        await CreateValidator().ValidateAsync(CommandWith(ServedCleanerId));

        _orderRepository.Verify(
            r => r.UserHasCompletedOrderWithEmployeeAsync(
                UserId, ServedCleanerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── The write ──

    [Fact]
    public async Task An_Eligible_Preference_Is_Stored_On_The_Template()
    {
        var template = await CreateTemplateAsync(ServedCleanerId);

        Assert.Equal(ServedCleanerId, template.PreferredEmployeeId);
    }

    [Fact]
    public async Task The_Created_Template_Response_Carries_The_Preference()
    {
        RecurringBookingTemplate? added = null;
        _templateRepository
            .Setup(r => r.Add(It.IsAny<RecurringBookingTemplate>()))
            .Callback((RecurringBookingTemplate t) => added = t);

        var result = await CreateHandler().Handle(CommandWith(ServedCleanerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(ServedCleanerId, result.Value!.PreferredEmployeeId);
    }

    [Fact]
    public async Task A_Template_Created_Without_A_Preference_Stores_None()
    {
        var template = await CreateTemplateAsync(null);

        Assert.Null(template.PreferredEmployeeId);
    }

    /// <summary>
    /// An empty string is not a preference. The column is nullable and every downstream predicate keys
    /// on null, so normalizing at the factory keeps "" out of the sweep — mirroring <c>Order.Create</c>.
    /// </summary>
    [Fact]
    public async Task An_Empty_Preference_Is_Normalized_To_None()
    {
        var template = await CreateTemplateAsync(string.Empty);

        Assert.Null(template.PreferredEmployeeId);
    }

    private async Task<RecurringBookingTemplate> CreateTemplateAsync(string? preferredEmployeeId)
    {
        RecurringBookingTemplate? added = null;
        _templateRepository
            .Setup(r => r.Add(It.IsAny<RecurringBookingTemplate>()))
            .Callback((RecurringBookingTemplate t) => added = t);

        var result = await CreateHandler().Handle(
            CommandWith(preferredEmployeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return added!;
    }

    private CreateRecurringBooking.Validator CreateValidator() =>
        new(_orderRepository.Object, _session.Object);

    private CreateRecurringBooking.Handler CreateHandler() =>
        new(
            _templateRepository.Object,
            _savedAddressRepository.Object,
            _membershipRepository.Object,
            _session.Object);

    private static CreateRecurringBooking.Command CommandWith(string? preferredEmployeeId) =>
        new(
            Frequency: (int)RecurrenceFrequency.Weekly,
            DayOfWeek: (int)System.DayOfWeek.Tuesday,
            TimeOfDay: "09:00",
            Rooms: 2,
            Bathrooms: 1,
            SavedAddressId: SavedAddressId,
            SelectedServiceIds: ["service-1"],
            SelectedPackageIds: [],
            PaymentType: (int)PaymentType.Card,
            StartsOn: DateTime.UtcNow.AddDays(3),
            EndsOn: null,
            PreferredEmployeeId: preferredEmployeeId);

    private static SavedAddress ArrangeSavedAddress()
    {
        var address = Address.Create("Dlouhá 12", "Praha", "11000", "country-cz");
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        typeof(SavedAddress).GetProperty(nameof(SavedAddress.Address))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(saved, [address]);
        return saved;
    }

    private static UserMembership ArrangeActiveMembership()
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
}
