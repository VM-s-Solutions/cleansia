using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// <c>UpdateSchedule</c> rewrites every schedule field and clears the materialisation watermark, so an
/// update with all fields changed IS a create wearing an old id. Without an entitlement link on this
/// chain, one paid month buys a permanently re-specifiable scheduling engine: subscribe, create, cancel,
/// then POST Update with a new frequency, service set and address forever.
///
/// <para>The owner ruling for a lapsed subscriber (<c>Q-PLUS-04</c>) is "keep generating, at full price"
/// — that PRESERVES a schedule; it does not license authoring a new one.</para>
///
/// <para>The ordering cases below are the point of putting the check on the EXISTING chain rather than in
/// a second <c>RuleFor</c>: FluentValidation's class-level default is <c>Continue</c>, so a parallel chain
/// would answer "you need Plus" for a template id that does not exist or belongs to someone else, leaking
/// entitlement state onto a path S3 requires to resolve as not-found.</para>
/// </summary>
public class UpdateRecurringBookingMembershipGuardTests
{
    private const string UserId = "user-plus-update";
    private const string OtherUserId = "user-someone-else";
    private const string TemplateId = "template-1";
    private const string SavedAddressId = "saved-address-1";

    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public UpdateRecurringBookingMembershipGuardTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _templateRepository
            .Setup(r => r.ExistsAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _templateRepository
            .Setup(r => r.GetByIdAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArrangeTemplate(UserId));
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
    }

    [Fact]
    public async Task A_Lapsed_Subscriber_Cannot_Re_Author_Their_Schedule()
    {
        var result = await CreateValidator().ValidateAsync(ValidCommand());

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.RecurringTemplateMembershipRequired, failure.ErrorMessage);
        Assert.Equal(nameof(UpdateRecurringBooking.Command.TemplateId), failure.PropertyName);
    }

    [Fact]
    public async Task An_Active_Member_May_Re_Author_Their_Schedule()
    {
        ArrangeActiveMembership();

        var result = await CreateValidator().ValidateAsync(ValidCommand());

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task A_Template_That_Does_Not_Exist_Answers_NotFound_Not_MembershipRequired()
    {
        var result = await CreateValidator().ValidateAsync(ValidCommand() with { TemplateId = "no-such-template" });

        Assert.False(result.IsValid);
        Assert.Equal(
            BusinessErrorMessage.RecurringTemplateNotFound,
            Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Someone_Elses_Template_Answers_NotOwned_Not_MembershipRequired()
    {
        _templateRepository
            .Setup(r => r.GetByIdAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArrangeTemplate(OtherUserId));

        var result = await CreateValidator().ValidateAsync(ValidCommand());

        Assert.False(result.IsValid);
        Assert.Equal(
            BusinessErrorMessage.RecurringTemplateNotOwnedByUser,
            Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task The_Entitlement_Question_Is_Scoped_To_The_Session_User()
    {
        ArrangeActiveMembership();

        await CreateValidator().ValidateAsync(ValidCommand());

        _membershipRepository.Verify(
            r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private UpdateRecurringBooking.Validator CreateValidator() =>
        new(_templateRepository.Object, _membershipRepository.Object, _session.Object);

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

    private static RecurringBookingTemplate ArrangeTemplate(string ownerUserId)
    {
        var template = RecurringBookingTemplate.Create(
            userId: ownerUserId,
            frequency: RecurrenceFrequency.Monthly,
            dayOfWeek: System.DayOfWeek.Monday,
            timeOfDay: new TimeOnly(8, 0),
            rooms: 1,
            bathrooms: 1,
            savedAddressId: SavedAddressId,
            selectedServiceIds: ["service-1"],
            selectedPackageIds: [],
            paymentType: PaymentType.Card,
            startsOn: DateTime.UtcNow.AddDays(1));
        template.Id = TemplateId;
        return template;
    }

    private static UpdateRecurringBooking.Command ValidCommand() =>
        new(
            TemplateId: TemplateId,
            Frequency: (int)RecurrenceFrequency.Weekly,
            DayOfWeek: (int)System.DayOfWeek.Tuesday,
            TimeOfDay: "09:00",
            Rooms: 4,
            Bathrooms: 2,
            SavedAddressId: SavedAddressId,
            SelectedServiceIds: ["service-2"],
            SelectedPackageIds: [],
            PaymentType: (int)PaymentType.Card,
            StartsOn: DateTime.UtcNow.AddDays(3));
}
