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
/// Recurring schedules are a paid Cleansia Plus perk. Every UI gates the entry point, but a gate that
/// only lives in the client is not a gate — a direct POST to /api/RecurringBooking/Create carries the
/// same <c>CanManageRecurringBookings</c> role claim every signed-in customer has, so before this
/// guard a non-member could take the perk for free. These pin the SERVER-side entitlement check:
/// no active membership at create time =&gt; failure + nothing added to the repository.
/// </summary>
public class CreateRecurringBookingMembershipGuardTests
{
    private const string UserId = "user-plus-1";
    private const string SavedAddressId = "saved-address-1";

    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public CreateRecurringBookingMembershipGuardTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _savedAddressRepository
            .Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ArrangeSavedAddress()]);
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
    }

    private CreateRecurringBooking.Handler CreateHandler() =>
        new(
            _templateRepository.Object,
            _savedAddressRepository.Object,
            _membershipRepository.Object,
            _session.Object);

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

    private void ArrangeActiveMembership()
    {
        var plan = MembershipPlan.Create(
            code: "PLUS",
            name: "Cleansia Plus",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_plus",
            discountPercentage: 10m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true);
        var membership = UserMembership.Create(
            userId: UserId,
            membershipPlanId: plan.Id,
            stripeSubscriptionId: "sub_1",
            currentPeriodStart: DateTime.UtcNow.AddDays(-1),
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
    }

    private static CreateRecurringBooking.Command ValidCommand() =>
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
            StartsOn: DateTime.UtcNow.AddDays(3));

    [Fact]
    public async Task Handle_NoActiveMembership_FailsWithMembershipRequired()
    {
        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RecurringTemplateMembershipRequired, result.Error.Message);
    }

    [Fact]
    public async Task Handle_NoActiveMembership_PersistsNothing()
    {
        await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        _templateRepository.Verify(r => r.Add(It.IsAny<RecurringBookingTemplate>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoActiveMembership_RejectsBeforeTouchingTheAddress()
    {
        await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        _savedAddressRepository.Verify(
            r => r.GetByUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ActiveMembership_CreatesTheTemplate()
    {
        ArrangeActiveMembership();

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _templateRepository.Verify(r => r.Add(It.IsAny<RecurringBookingTemplate>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MembershipCheckIsScopedToTheSessionUser()
    {
        ArrangeActiveMembership();

        await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        _membershipRepository.Verify(
            r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
