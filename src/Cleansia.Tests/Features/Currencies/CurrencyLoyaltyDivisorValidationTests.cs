using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Currencies;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Currencies;

/// <summary>
/// A currency's loyalty divisor is optional -- null is "earns nothing yet" -- but when set it must be
/// positive: zero is a division by zero and a negative is a negative earn, and a zero that slipped
/// through would be treated as "no divisor" by the earn while the admin believes the currency is
/// configured (T-0703). And an ACTIVE currency may not have its divisor cleared at all: activation
/// refuses a currency without one, and clearing it afterwards would reopen the same hole -- every
/// order completed in the window earns nothing, permanently.
/// </summary>
public class CurrencyLoyaltyDivisorValidationTests
{
    private const string CurrencyId = "currency-1";

    private readonly Mock<ICurrencyRepository> _currencies = new();

    public CurrencyLoyaltyDivisorValidationTests()
    {
        _currencies.Setup(r => r.ExistsAsync(CurrencyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _currencies.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Currency?)null);
        _currencies.Setup(r => r.ExistsWithCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(-1.0, false)]
    [InlineData(null, true)]
    [InlineData(0.40, true)]
    [InlineData(10.0, true)]
    public async Task Update_Divisor_Must_Be_Positive_When_Set(double? divisor, bool valid)
    {
        var command = new UpdateCurrency.Command(CurrencyId, "EUR", "€", "Euro", (decimal?)divisor);

        var result = await new UpdateCurrency.Validator(_currencies.Object).ValidateAsync(command);

        Assert.Equal(valid, result.IsValid);
        Assert.Equal(!valid, result.Errors.Any(e =>
            e.PropertyName == nameof(UpdateCurrency.Command.LoyaltyPointsDivisor)
            && e.ErrorMessage == BusinessErrorMessage.MustBePositive));
    }

    [Fact]
    public async Task Update_Refuses_Clearing_The_Divisor_On_An_Active_Currency()
    {
        ArrangeStored(isActive: true, divisor: 10m);
        var command = new UpdateCurrency.Command(CurrencyId, "EUR", "€", "Euro", LoyaltyPointsDivisor: null);

        var result = await new UpdateCurrency.Validator(_currencies.Object).ValidateAsync(command);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.CurrencyLoyaltyDivisorMissing, error.ErrorMessage);
        Assert.Equal(nameof(UpdateCurrency.Command.LoyaltyPointsDivisor), error.PropertyName);
    }

    [Fact]
    public async Task Update_Allows_A_Null_Divisor_On_An_Inactive_Currency()
    {
        ArrangeStored(isActive: false, divisor: 10m);
        var command = new UpdateCurrency.Command(CurrencyId, "EUR", "€", "Euro", LoyaltyPointsDivisor: null);

        var result = await new UpdateCurrency.Validator(_currencies.Object).ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Update_Allows_Changing_The_Divisor_On_An_Active_Currency()
    {
        ArrangeStored(isActive: true, divisor: 10m);
        var command = new UpdateCurrency.Command(CurrencyId, "EUR", "€", "Euro", LoyaltyPointsDivisor: 25m);

        var result = await new UpdateCurrency.Validator(_currencies.Object).ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    /// <summary>A non-positive value is refused for being non-positive; the active-market rule does not also fire.</summary>
    [Fact]
    public async Task Update_On_An_Active_Currency_Reports_A_Zero_Divisor_Once()
    {
        ArrangeStored(isActive: true, divisor: 10m);
        var command = new UpdateCurrency.Command(CurrencyId, "EUR", "€", "Euro", LoyaltyPointsDivisor: 0m);

        var result = await new UpdateCurrency.Validator(_currencies.Object).ValidateAsync(command);

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.MustBePositive, error.ErrorMessage);
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(null, true)]
    [InlineData(10.0, true)]
    public async Task Create_Divisor_Must_Be_Positive_When_Set(double? divisor, bool valid)
    {
        var command = new CreateCurrency.Command("EUR", "€", "Euro", (decimal?)divisor);

        var result = await new CreateCurrency.Validator(_currencies.Object).ValidateAsync(command);

        Assert.Equal(valid, result.IsValid);
    }

    /// <summary>
    /// The apology credit is authored beside the divisor (ADR-0060 D1). Null is "no credit in this
    /// currency" and is legal on an active market — the copy has a refund-only variant for it — so
    /// only a non-positive figure is refused.
    /// </summary>
    [Theory]
    [InlineData(-1.0, false)]
    [InlineData(0.0, false)]
    [InlineData(null, true)]
    [InlineData(250.0, true)]
    public async Task Update_No_Show_Credit_Must_Be_Positive_When_Set(double? credit, bool valid)
    {
        ArrangeStored(isActive: true, divisor: 10m);
        var command = new UpdateCurrency.Command(CurrencyId, "EUR", "€", "Euro", 10m, (decimal?)credit);

        var result = await new UpdateCurrency.Validator(_currencies.Object).ValidateAsync(command);

        Assert.Equal(valid, result.IsValid);
        Assert.Equal(!valid, result.Errors.Any(e =>
            e.PropertyName == nameof(UpdateCurrency.Command.NoShowCredit)
            && e.ErrorMessage == BusinessErrorMessage.MustBePositive));
    }

    [Theory]
    [InlineData(-1.0, false)]
    [InlineData(null, true)]
    [InlineData(250.0, true)]
    public async Task Create_No_Show_Credit_Must_Be_Positive_When_Set(double? credit, bool valid)
    {
        var command = new CreateCurrency.Command("EUR", "€", "Euro", 10m, (decimal?)credit);

        var result = await new CreateCurrency.Validator(_currencies.Object).ValidateAsync(command);

        Assert.Equal(valid, result.IsValid);
    }

    private void ArrangeStored(bool isActive, decimal? divisor)
    {
        var currency = Currency.Create("EUR", "€", "Euro");
        currency.Id = CurrencyId;
        currency.IsActive = isActive;
        currency.SetLoyaltyPointsDivisor(divisor);
        _currencies.Setup(r => r.GetByIdAsync(CurrencyId, It.IsAny<CancellationToken>())).ReturnsAsync(currency);
    }
}
