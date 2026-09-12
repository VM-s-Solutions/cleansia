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
/// configured (T-0703).
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
}
