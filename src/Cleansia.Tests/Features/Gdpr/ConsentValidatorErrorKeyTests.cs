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

    /// <summary>
    /// Owner ruling 2026-09-28: the terms and the privacy policy are the only acceptances the consent endpoint
    /// records. The cookie banner's analytics and marketing categories write nothing, and a cleaner's
    /// documents are accepted through their own endpoint, which echoes the text shown.
    /// </summary>
    [Theory]
    [InlineData(ConsentType.TermsOfService)]
    [InlineData(ConsentType.PrivacyPolicy)]
    public void The_Terms_And_The_Privacy_Policy_Can_Be_Accepted(ConsentType consentType)
    {
        new GrantConsent.Validator().TestValidate(new GrantConsent.Command(consentType))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(ConsentType.MarketingEmails)]
    [InlineData(ConsentType.DataProcessing)]
    [InlineData(ConsentType.CleanerFrameworkContract)]
    [InlineData(ConsentType.SelfBillingAgreement)]
    [InlineData(ConsentType.CleanerDataProcessingAgreement)]
    public void No_Other_Type_Is_Granted_Through_The_Consent_Endpoint(ConsentType consentType)
    {
        new GrantConsent.Validator().TestValidate(new GrantConsent.Command(consentType))
            .ShouldHaveValidationErrorFor(c => c.ConsentType)
            .WithErrorMessage(BusinessErrorMessage.ConsentNotEditable);
    }

    /// <summary>An accepted text is shown read-only (owner ruling 2026-09-28); a consent with no text stays withdrawable.</summary>
    [Theory]
    [InlineData(ConsentType.TermsOfService)]
    [InlineData(ConsentType.PrivacyPolicy)]
    [InlineData(ConsentType.CleanerFrameworkContract)]
    [InlineData(ConsentType.SelfBillingAgreement)]
    [InlineData(ConsentType.CleanerDataProcessingAgreement)]
    public void An_Accepted_Document_Cannot_Be_Withdrawn(ConsentType consentType)
    {
        new WithdrawConsent.Validator().TestValidate(new WithdrawConsent.Command(consentType))
            .ShouldHaveValidationErrorFor(c => c.ConsentType)
            .WithErrorMessage(BusinessErrorMessage.ConsentNotEditable);
    }

    [Theory]
    [InlineData(ConsentType.MarketingEmails)]
    [InlineData(ConsentType.DataProcessing)]
    public void A_Consent_Without_A_Document_Can_Be_Withdrawn(ConsentType consentType)
    {
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
