using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Tests.Features.Orders;
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
    // Only reached when a command names a preferred employee, which these membership-gate cases
    // never do — the validator still needs one to construct.
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();

    public UpdateRecurringBookingMembershipGuardTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _savedAddressRepository.Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Cleansia.Core.Domain.Users.SavedAddress>());
        _templateRepository
            .Setup(r => r.ExistsAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _templateRepository
            .Setup(r => r.GetByIdForOwnerAsync(TemplateId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArrangeTemplate(UserId));
        _membershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
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
            .Setup(r => r.GetByIdForOwnerAsync(TemplateId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Validation_Reads_The_Owned_Template_Once_With_The_Caller_Token()
    {
        ArrangeActiveMembership();
        using var cancellation = new CancellationTokenSource();

        var result = await CreateValidator().ValidateAsync(ValidCommand(), cancellation.Token);

        Assert.True(result.IsValid);
        _templateRepository.Verify(
            r => r.GetByIdForOwnerAsync(TemplateId, UserId, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task A_Missing_Template_Is_Reused_Without_An_Entitlement_Question()
    {
        var result = await CreateValidator().ValidateAsync(ValidCommand() with { TemplateId = "missing" });

        Assert.Equal(BusinessErrorMessage.RecurringTemplateNotFound, Assert.Single(result.Errors).ErrorMessage);
        _templateRepository.Verify(
            r => r.GetByIdForOwnerAsync("missing", UserId, It.IsAny<CancellationToken>()), Times.Once);
        _membershipRepository.Verify(
            r => r.GetEntitledForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Foreign_Template_Is_Reused_Without_An_Entitlement_Question()
    {
        _templateRepository
            .Setup(r => r.GetByIdForOwnerAsync(TemplateId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArrangeTemplate(OtherUserId));

        var result = await CreateValidator().ValidateAsync(ValidCommand());

        Assert.Equal(BusinessErrorMessage.RecurringTemplateNotOwnedByUser, Assert.Single(result.Errors).ErrorMessage);
        _templateRepository.Verify(
            r => r.GetByIdForOwnerAsync(TemplateId, UserId, It.IsAny<CancellationToken>()), Times.Once);
        _membershipRepository.Verify(
            r => r.GetEntitledForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Cached_Missing_Result_Does_Not_Outlive_Its_Validation_Context()
    {
        ArrangeActiveMembership();
        RecurringBookingTemplate? stored = null;
        _templateRepository
            .Setup(r => r.GetByIdForOwnerAsync(TemplateId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stored);
        var validator = CreateValidator();

        var missing = await validator.ValidateAsync(ValidCommand());
        stored = ArrangeTemplate(UserId);
        var present = await validator.ValidateAsync(ValidCommand());

        Assert.Equal(BusinessErrorMessage.RecurringTemplateNotFound, Assert.Single(missing.Errors).ErrorMessage);
        Assert.True(present.IsValid);
        _templateRepository.Verify(
            r => r.GetByIdForOwnerAsync(TemplateId, UserId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Reusing_A_Validator_Still_Reads_Each_Current_Owner_And_Template()
    {
        ArrangeActiveMembership();
        ArrangeActiveMembership(OtherUserId);
        _savedAddressRepository.Setup(r => r.GetByUserAsync(OtherUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Cleansia.Core.Domain.Users.SavedAddress>());
        var currentUser = UserId;
        _session.Setup(s => s.GetUserId()).Returns(() => currentUser);
        var otherTemplate = ArrangeTemplate(OtherUserId);
        otherTemplate.Id = "template-2";
        _templateRepository
            .Setup(r => r.GetByIdForOwnerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, string owner, CancellationToken _) =>
                id == TemplateId && owner == UserId ? ArrangeTemplate(UserId)
                : id == otherTemplate.Id && owner == OtherUserId ? otherTemplate : null);
        var validator = CreateValidator();

        var first = await validator.ValidateAsync(ValidCommand());
        currentUser = OtherUserId;
        var second = await validator.ValidateAsync(ValidCommand() with { TemplateId = otherTemplate.Id });
        var foreign = await validator.ValidateAsync(ValidCommand());

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.Equal(BusinessErrorMessage.RecurringTemplateNotFound, Assert.Single(foreign.Errors).ErrorMessage);
        _templateRepository.Verify(r => r.GetByIdForOwnerAsync(TemplateId, UserId, It.IsAny<CancellationToken>()), Times.Once);
        _templateRepository.Verify(r => r.GetByIdForOwnerAsync(otherTemplate.Id, OtherUserId, It.IsAny<CancellationToken>()), Times.Once);
        _templateRepository.Verify(r => r.GetByIdForOwnerAsync(TemplateId, OtherUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Cancelled_Template_Read_Is_Propagated_And_A_Later_Context_Reads_Again()
    {
        ArrangeActiveMembership();
        using var cancellation = new CancellationTokenSource();
        _templateRepository
            .Setup(r => r.GetByIdForOwnerAsync(TemplateId, UserId, cancellation.Token))
            .Returns((string _, string _, CancellationToken token) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<RecurringBookingTemplate?>(token);
            });
        var validator = CreateValidator();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => validator.ValidateAsync(ValidCommand(), cancellation.Token));
        _templateRepository
            .Setup(r => r.GetByIdForOwnerAsync(TemplateId, UserId, CancellationToken.None))
            .ReturnsAsync(ArrangeTemplate(UserId));
        var result = await validator.ValidateAsync(ValidCommand());

        Assert.True(result.IsValid);
        _templateRepository.Verify(r => r.GetByIdForOwnerAsync(TemplateId, UserId, cancellation.Token), Times.Once);
        _templateRepository.Verify(r => r.GetByIdForOwnerAsync(TemplateId, UserId, CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Retired_Selections_Pass_Only_If_The_Owned_Template_Already_Holds_Them(bool package, bool held)
    {
        ArrangeActiveMembership();
        var retiredService = CatalogueDoubles.Service("retired-service", 60);
        var retiredPackage = CatalogueDoubles.Package("retired-package");
        retiredService.IsActive = false;
        retiredPackage.IsActive = false;
        _templateRepository
            .Setup(r => r.GetByIdForOwnerAsync(TemplateId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ArrangeTemplate(UserId,
                held && !package ? [retiredService.Id] : [], held && package ? [retiredPackage.Id] : []));
        var command = ValidCommand() with
        {
            SelectedServiceIds = package ? [] : [retiredService.Id],
            SelectedPackageIds = package ? [retiredPackage.Id] : [],
        };

        var result = await CreateValidator(CatalogueDoubles.Services(retiredService), CatalogueDoubles.Packages(retiredPackage))
            .ValidateAsync(command);

        Assert.Equal(held, result.IsValid);
        if (held) Assert.Empty(result.Errors);
        else Assert.Equal(package ? BusinessErrorMessage.InvalidSelectedPackage : BusinessErrorMessage.InvalidSelectedServices,
            Assert.Single(result.Errors).ErrorMessage);
    }

    private UpdateRecurringBooking.Validator CreateValidator(IServiceRepository? services = null, IPackageRepository? packages = null) =>
        new(_templateRepository.Object, _membershipRepository.Object, _session.Object,
            _orderRepository.Object, _savedAddressRepository.Object,
            OrderMarketDoubles.Trading(CreateOrderTestData.DefaultCurrency()), OrderMarketDoubles.Servicing("country-cz"),
            services ?? CatalogueDoubles.Services(), packages ?? CatalogueDoubles.Packages(),
            Mock.Of<IReceivableRepository>());

    private void ArrangeActiveMembership(string ownerUserId = UserId)
    {
        var plan = MembershipPlan.Create(
            code: "PLUS",
            name: "Cleansia Plus",
            discountPercentage: 10m,
            allowsExpressUpgrade: true);
        var membership = UserMembership.Create(
            userId: ownerUserId,
            membershipPlanId: plan.Id,
            currencyId: "currency-czk",
            stripeSubscriptionId: "sub_1",
            currentPeriodStart: DateTime.UtcNow.AddDays(-1),
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
        _membershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(ownerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
    }

    private static RecurringBookingTemplate ArrangeTemplate(string ownerUserId,
        IReadOnlyList<string>? selectedServiceIds = null, IReadOnlyList<string>? selectedPackageIds = null)
    {
        var template = RecurringBookingTemplate.Create(
            userId: ownerUserId,
            frequency: RecurrenceFrequency.Monthly,
            dayOfWeek: System.DayOfWeek.Monday,
            timeOfDay: new TimeOnly(8, 0),
            rooms: 1,
            bathrooms: 1,
            savedAddressId: SavedAddressId,
            selectedServiceIds: selectedServiceIds ?? ["service-1"],
            selectedPackageIds: selectedPackageIds ?? [],
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
