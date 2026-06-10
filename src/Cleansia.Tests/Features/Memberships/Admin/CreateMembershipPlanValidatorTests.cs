using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships.Admin;
using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Tests.Features.Memberships.Admin;

/// <summary>
/// AC6 — field-level range validation on create. Negative price, out-of-range
/// discount %, negative trial / cancellation window, empty Stripe Price id, and
/// an out-of-range billing interval each fail with the mapped BusinessErrorMessage
/// before any persistence. Written TEST-FIRST.
/// </summary>
public class CreateMembershipPlanValidatorTests
{
    private static readonly CreateMembershipPlan.Validator Validator = new();

    private static CreateMembershipPlan.Command Valid() =>
        new(
            Code: "PLUS_MONTHLY",
            Name: "Plus Monthly",
            BillingInterval: BillingInterval.Monthly,
            MonthlyPriceCzk: 199m,
            StripePriceId: "price_plus_monthly",
            DiscountPercentage: 5m,
            FreeCancellationWindowHours: 4,
            TrialPeriodDays: 0,
            AllowsExpressUpgrade: true);

    [Fact]
    public async Task Valid_Command_Passes()
    {
        var result = await Validator.ValidateAsync(Valid());
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task NegativePrice_Fails_MustBePositive()
    {
        var result = await Validator.ValidateAsync(Valid() with { MonthlyPriceCzk = -1m });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateMembershipPlan.Command.MonthlyPriceCzk)
            && e.ErrorMessage == BusinessErrorMessage.MustBePositive);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task DiscountOutOfRange_Fails(decimal discount)
    {
        var result = await Validator.ValidateAsync(Valid() with { DiscountPercentage = discount });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateMembershipPlan.Command.DiscountPercentage)
            && e.ErrorMessage == BusinessErrorMessage.MembershipPlanDiscountOutOfRange);
    }

    [Fact]
    public async Task NegativeTrial_Fails_MustBePositive()
    {
        var result = await Validator.ValidateAsync(Valid() with { TrialPeriodDays = -1 });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateMembershipPlan.Command.TrialPeriodDays));
    }

    [Fact]
    public async Task NegativeCancellationWindow_Fails_MustBePositive()
    {
        var result = await Validator.ValidateAsync(Valid() with { FreeCancellationWindowHours = -1 });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateMembershipPlan.Command.FreeCancellationWindowHours));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EmptyStripePriceId_Fails_Required(string? stripePriceId)
    {
        var result = await Validator.ValidateAsync(Valid() with { StripePriceId = stripePriceId! });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateMembershipPlan.Command.StripePriceId)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task OutOfRangeBillingInterval_Fails_InvalidEnum()
    {
        var result = await Validator.ValidateAsync(Valid() with { BillingInterval = (BillingInterval)99 });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateMembershipPlan.Command.BillingInterval)
            && e.ErrorMessage == BusinessErrorMessage.InvalidEnumValue);
    }
}
