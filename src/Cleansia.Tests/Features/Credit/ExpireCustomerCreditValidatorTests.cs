using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Credit.Admin;
using Cleansia.Core.Domain.Repositories;
using FluentValidation.TestHelper;
using Moq;

namespace Cleansia.Tests.Features.Credit;

/// <summary>
/// A discharge names the currency it drains. Credit is held per currency, so "expire this customer's
/// balance" is not one action once they hold two; the admin says which, and the platform checks it is
/// a real currency — but NOT that it is still operated, because a balance stranded in a switched-off
/// currency is exactly the one that must still be dischargeable before erasure.
/// </summary>
public class ExpireCustomerCreditValidatorTests
{
    private const string UserId = "01USERCREDIT00000000000001";
    private const string CurrencyId = "01CURRENCYEUR0000000000001";

    private static ExpireCustomerCredit.Validator ValidatorFor(
        bool userExists = true, bool currencyExists = true)
    {
        var users = new Mock<IUserRepository>();
        users
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userExists);

        var currencies = new Mock<ICurrencyRepository>();
        currencies
            .Setup(r => r.ExistsAsync(CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currencyExists);

        return new ExpireCustomerCredit.Validator(users.Object, currencies.Object);
    }

    private static ExpireCustomerCredit.Command Valid(
        string currencyId = CurrencyId,
        string note = "Customer asked to leave.",
        string requestId = "req-1") =>
        new(UserId, currencyId, note, requestId);

    [Fact]
    public async Task ADischargeWithoutACurrencyIsRefused()
    {
        var result = await ValidatorFor().TestValidateAsync(Valid(currencyId: ""));
        result.ShouldHaveValidationErrorFor(x => x.CurrencyId)
            .WithErrorMessage(BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task ADischargeInAnUnknownCurrencyIsRefused()
    {
        var result = await ValidatorFor(currencyExists: false).TestValidateAsync(Valid());
        result.ShouldHaveValidationErrorFor(x => x.CurrencyId)
            .WithErrorMessage(BusinessErrorMessage.CurrencyNotFound);
    }

    [Fact]
    public async Task ADischargeInAKnownCurrencyPasses()
    {
        var result = await ValidatorFor().TestValidateAsync(Valid());
        result.ShouldNotHaveValidationErrorFor(x => x.CurrencyId);
    }

    /// <summary>
    /// Anti-vacuity for the currency gate: the existence check must be the currency's, not the user's.
    /// </summary>
    [Fact]
    public async Task AnUnknownUserIsRefusedOnTheUserNotTheCurrency()
    {
        var result = await ValidatorFor(userExists: false).TestValidateAsync(Valid());
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage(BusinessErrorMessage.UserNotFound);
        result.ShouldNotHaveValidationErrorFor(x => x.CurrencyId);
    }
}
