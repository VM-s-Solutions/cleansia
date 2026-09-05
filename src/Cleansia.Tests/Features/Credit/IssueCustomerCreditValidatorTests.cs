using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Credit.Admin;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Repositories;
using FluentValidation.TestHelper;
using Moq;

namespace Cleansia.Tests.Features.Credit;

/// <summary>
/// What an admin may put on a customer's balance. Money out, so every gate is asserted by the key it
/// answers with, not merely by "invalid".
/// </summary>
public class IssueCustomerCreditValidatorTests
{
    private const string UserId = "01USERCREDIT00000000000001";

    private static IssueCustomerCredit.Validator ValidatorFor(bool userExists = true)
    {
        var users = new Mock<IUserRepository>();
        users
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userExists);
        return new IssueCustomerCredit.Validator(users.Object);
    }

    private static IssueCustomerCredit.Command Valid(
        decimal amount = 500m,
        CreditTransactionReason reason = CreditTransactionReason.Goodwill,
        string note = "Second clean in a row went wrong.",
        string requestId = "req-1") =>
        new(UserId, amount, reason, note, requestId);

    [Fact]
    public async Task AWellFormedGrantPasses()
    {
        var result = await ValidatorFor().TestValidateAsync(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task AGrantToANonExistentUserIsRefused()
    {
        var result = await ValidatorFor(userExists: false).TestValidateAsync(Valid());
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage(BusinessErrorMessage.UserNotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ANonPositiveGrantIsRefused(decimal amount)
    {
        var result = await ValidatorFor().TestValidateAsync(Valid(amount: amount));
        result.ShouldHaveValidationErrorFor(x => x.Amount)
            .WithErrorMessage(BusinessErrorMessage.MustBePositive);
    }

    [Fact]
    public async Task AGrantAboveTheTypoCeilingIsRefused()
    {
        var result = await ValidatorFor()
            .TestValidateAsync(Valid(amount: IssueCustomerCredit.Validator.SanityCap + 0.01m));

        result.ShouldHaveValidationErrorFor(x => x.Amount)
            .WithErrorMessage(BusinessErrorMessage.CreditAmountExceedsSanityCap);
    }

    /// <summary>
    /// A balance is spent against Stripe, which takes integer cents. A third of a cent on the balance
    /// is money the customer can never spend and the ledger never quite reconciles.
    /// </summary>
    [Fact]
    public async Task AGrantFinerThanOneMinorUnitIsRefused()
    {
        var result = await ValidatorFor().TestValidateAsync(Valid(amount: 10.005m));

        result.ShouldHaveValidationErrorFor(x => x.Amount)
            .WithErrorMessage(BusinessErrorMessage.CreditAmountNotWholeMinorUnits);
    }

    /// <summary>
    /// THE ONE WORTH HAVING: the spend-side reasons describe money LEAVING the balance. Accepting one
    /// here writes a POSITIVE <c>OrderPayment</c> row, which reads as "credited for paying us" and
    /// makes the ledger's own vocabulary a lie.
    /// </summary>
    [Theory]
    [InlineData(CreditTransactionReason.OrderPayment)]
    [InlineData(CreditTransactionReason.OrderPaymentReturned)]
    public async Task ASpendSideReasonCannotIssueCredit(CreditTransactionReason reason)
    {
        var result = await ValidatorFor().TestValidateAsync(Valid(reason: reason));

        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorMessage(BusinessErrorMessage.CreditReasonNotIssuable);
    }

    [Theory]
    [InlineData(CreditTransactionReason.DisputeSettlement)]
    [InlineData(CreditTransactionReason.CleanerNoShow)]
    [InlineData(CreditTransactionReason.Goodwill)]
    public async Task TheThreeIssueSideReasonsAreAccepted(CreditTransactionReason reason)
    {
        var result = await ValidatorFor().TestValidateAsync(Valid(reason: reason));
        result.ShouldNotHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public async Task AnUnknownReasonIsRefused()
    {
        var result = await ValidatorFor().TestValidateAsync(Valid(reason: (CreditTransactionReason)99));

        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorMessage(BusinessErrorMessage.InvalidEnumValue);
    }

    [Fact]
    public async Task AGrantWithNoStatedReasonIsRefused()
    {
        var result = await ValidatorFor().TestValidateAsync(Valid(note: "   "));

        result.ShouldHaveValidationErrorFor(x => x.Note)
            .WithErrorMessage(BusinessErrorMessage.Required);
    }

    /// <summary>
    /// S7a. Without a token the ledger's unique index has nothing to collide on, and a double-submit
    /// grants the money twice.
    /// </summary>
    [Fact]
    public async Task AGrantWithNoIdempotencyTokenIsRefused()
    {
        var result = await ValidatorFor().TestValidateAsync(Valid(requestId: ""));

        result.ShouldHaveValidationErrorFor(x => x.RequestId)
            .WithErrorMessage(BusinessErrorMessage.Required);
    }

    /// <summary>
    /// Bounded to the persisted column width (120). A token longer than the column is truncated on
    /// write, so two different tokens can silently become one key — or one key two.
    /// </summary>
    [Fact]
    public async Task AnOverlongIdempotencyTokenIsRefused()
    {
        var result = await ValidatorFor().TestValidateAsync(Valid(requestId: new string('k', 121)));

        result.ShouldHaveValidationErrorFor(x => x.RequestId)
            .WithErrorMessage(BusinessErrorMessage.MaxLength);
    }
}
