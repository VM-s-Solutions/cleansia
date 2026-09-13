using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Validations;
using FluentValidation.TestHelper;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// A refused consent command is a customer audit row whose <c>ErrorCode</c> is the refusal KEY
/// (ADR-0062 D1). These two validators were the only customer-marked rules without a
/// <c>BusinessErrorMessage</c> key, so a <c>consentType: 99</c> stored FluentValidation's English
/// sentence, submitted integer included, where every other row stores a key the error-code filter can
/// match and a client can translate.
/// </summary>
public sealed class ConsentValidatorErrorKeyTests
{
    private const ConsentType Unknown = (ConsentType)99;

    [Fact]
    public void An_Unknown_Consent_Type_On_Grant_Is_Refused_With_The_Key()
    {
        var result = new GrantConsent.Validator().TestValidate(new GrantConsent.Command(Unknown));

        result.ShouldHaveValidationErrorFor(c => c.ConsentType)
            .WithErrorMessage(BusinessErrorMessage.InvalidEnumValue);
    }

    [Fact]
    public void An_Unknown_Consent_Type_On_Withdraw_Is_Refused_With_The_Key()
    {
        var result = new WithdrawConsent.Validator().TestValidate(new WithdrawConsent.Command(Unknown));

        result.ShouldHaveValidationErrorFor(c => c.ConsentType)
            .WithErrorMessage(BusinessErrorMessage.InvalidEnumValue);
    }

    [Theory]
    [InlineData(ConsentType.TermsOfService)]
    [InlineData(ConsentType.PrivacyPolicy)]
    [InlineData(ConsentType.MarketingEmails)]
    [InlineData(ConsentType.DataProcessing)]
    public void Every_Known_Consent_Type_Passes_Both_Validators(ConsentType consentType)
    {
        new GrantConsent.Validator().TestValidate(new GrantConsent.Command(consentType))
            .ShouldNotHaveAnyValidationErrors();
        new WithdrawConsent.Validator().TestValidate(new WithdrawConsent.Command(consentType))
            .ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>The row's error code, as the pipeline resolves it from the validator's own failure.</summary>
    [Fact]
    public void The_Failure_Row_Carries_The_Key_Not_A_Sentence_With_The_Submitted_Integer()
    {
        var failure = new GrantConsent.Validator().Validate(new GrantConsent.Command(Unknown)).Errors.Single();
        var result = ValidationResult.WithErrors([new Error(failure.PropertyName, failure.ErrorMessage)]);

        Assert.Equal(BusinessErrorMessage.InvalidEnumValue, AuditErrorCode.Resolve(result));
    }
}
