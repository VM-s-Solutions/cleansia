using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// Locks the B5 contract on <see cref="CreateMembershipCheckoutSession.Handler"/>: the
/// <see cref="BusinessErrorMessage.UserNotFound"/> failure names the OFFENDING field (the
/// session-derived user id), not <c>nameof(Command)</c> (consistency.md B5), mirroring the sibling
/// <see cref="CreateMembershipSubscription"/> handler. The web/Checkout success path still returns its
/// <see cref="CreateMembershipCheckoutSession.Response"/> shape.
/// </summary>
public class CreateMembershipCheckoutSessionContractLockTests
{
    private const string UserId = "user-1";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string StripeCustomerId = "cus_test_1";
    private const string StripePriceId = "price_test_1";
    private const string SuccessUrl = "https://app/success";
    private const string CancelUrl = "https://app/cancel";
    private const string CheckoutUrl = "https://checkout.stripe.test/session_1";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();

    public CreateMembershipCheckoutSessionContractLockTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var plan = MembershipPlan.Create(
            code: PlanCode,
            name: "Plus Monthly",
            monthlyPriceCzk: 199m,
            stripePriceId: StripePriceId,
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: 0);
        _planRepository
            .Setup(r => r.GetByCodeAsync(PlanCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
    }

    private void SetupUserWithStripeCustomer()
    {
        var user = User.CreateWithPassword("sub@example.com", "12345678Test!", "Sub", "Scriber");
        user.Id = UserId;
        user.AssignStripeCustomerId(StripeCustomerId);
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
    }

    private CreateMembershipCheckoutSession.Handler CreateHandler() =>
        new(
            _userRepository.Object,
            _membershipRepository.Object,
            _planRepository.Object,
            _session.Object,
            _stripe.Object,
            NullLogger<CreateMembershipCheckoutSession.Handler>.Instance);

    [Fact]
    public async Task UserNotFound_Failure_NamesOffendingUserField_NotCommand()
    {
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode, SuccessUrl, CancelUrl),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.UserNotFound, result.Error!.Message);
        Assert.Equal("userId", result.Error.Code);
        Assert.NotEqual(nameof(CreateMembershipCheckoutSession.Command), result.Error.Code);
    }

    [Fact]
    public async Task MembershipAlreadyActive_Failure_NamesOffendingField_NotType()
    {
        var user = User.CreateWithPassword("sub@example.com", "12345678Test!", "Sub", "Scriber");
        user.Id = UserId;
        user.AssignStripeCustomerId(StripeCustomerId);
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var plan = MembershipPlan.Create(
            code: PlanCode, name: "Plus Monthly", monthlyPriceCzk: 199m, stripePriceId: StripePriceId,
            discountPercentage: 5m, freeCancellationWindowHours: 4, allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly, trialPeriodDays: 0);
        var active = UserMembership.Create(UserId, plan.Id, "sub_active_1", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(active);

        var result = await CreateHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode, SuccessUrl, CancelUrl),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.MembershipAlreadyActive, result.Error!.Message);
        Assert.Equal(nameof(CreateMembershipCheckoutSession.Command.PlanCode), result.Error.Code);
        Assert.NotEqual(nameof(UserMembership), result.Error.Code);
    }

    [Fact]
    public async Task HappyPath_ReturnsCheckoutUrl()
    {
        SetupUserWithStripeCustomer();
        _stripe
            .Setup(c => c.CreateMembershipCheckoutSessionAsync(
                StripeCustomerId, StripePriceId, UserId, PlanCode, It.IsAny<int>(),
                SuccessUrl, CancelUrl, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckoutUrl);

        var result = await CreateHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode, SuccessUrl, CancelUrl),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CheckoutUrl, result.Value.CheckoutUrl);
    }
}
