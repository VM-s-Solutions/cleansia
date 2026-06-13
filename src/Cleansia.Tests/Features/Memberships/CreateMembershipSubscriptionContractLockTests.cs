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
/// Locks the LG-07 / B5 contract on <see cref="CreateMembershipSubscription.Handler"/>: the
/// <see cref="BusinessErrorMessage.UserNotFound"/> failure names the OFFENDING field (the
/// session-derived user id), not <c>nameof(Command)</c> (consistency.md B5), and the two subscribe
/// branches still return their respective <see cref="CreateMembershipSubscription.Response"/> shapes —
/// web/Checkout never reaches this handler, mobile drives both branches.
/// </summary>
public class CreateMembershipSubscriptionContractLockTests
{
    private const string UserId = "user-1";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string StripeCustomerId = "cus_test_1";
    private const string StripePriceId = "price_test_1";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();

    public CreateMembershipSubscriptionContractLockTests()
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

    private CreateMembershipSubscription.Handler CreateHandler() =>
        new(
            _userRepository.Object,
            _membershipRepository.Object,
            _planRepository.Object,
            _session.Object,
            _stripe.Object,
            NullLogger<CreateMembershipSubscription.Handler>.Instance);

    [Fact]
    public async Task UserNotFound_Failure_NamesOffendingUserField_NotCommand()
    {
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.UserNotFound, result.Error!.Message);
        Assert.Equal("userId", result.Error.Code);
        Assert.NotEqual(nameof(CreateMembershipSubscription.Command), result.Error.Code);
    }

    [Fact]
    public async Task UnconfirmedBranch_ReturnsSetupIntentAndEphemeralKey_WithEmptyMembershipId()
    {
        SetupUserWithStripeCustomer();
        _stripe
            .Setup(c => c.CreateSetupIntentAsync(StripeCustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SetupIntentResult("seti_1", "seti_secret_1"));
        _stripe
            .Setup(c => c.CreateEphemeralKeyAsync(StripeCustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ek_secret_1");

        var result = await CreateHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("seti_secret_1", result.Value.SetupIntentClientSecret);
        Assert.Equal("ek_secret_1", result.Value.EphemeralKey);
        Assert.Equal(StripeCustomerId, result.Value.StripeCustomerId);
        Assert.Equal(string.Empty, result.Value.MembershipId);
    }

    [Fact]
    public async Task ConfirmedBranch_ReturnsMembershipId_WithEmptySetupIntentAndEphemeralKey()
    {
        SetupUserWithStripeCustomer();
        _stripe
            .Setup(c => c.CreateSubscriptionAsync(
                StripeCustomerId, StripePriceId, It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionResult(
                SubscriptionId: "sub_confirmed_1",
                CurrentPeriodStart: DateTime.UtcNow,
                CurrentPeriodEnd: DateTime.UtcNow.AddMonths(1)));

        UserMembership? added = null;
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(m => added = m);

        var result = await CreateHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(added!.Id, result.Value.MembershipId);
        Assert.Equal(string.Empty, result.Value.SetupIntentClientSecret);
        Assert.Equal(string.Empty, result.Value.EphemeralKey);
        Assert.Equal(StripeCustomerId, result.Value.StripeCustomerId);
    }
}
