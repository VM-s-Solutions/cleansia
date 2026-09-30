using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum ConsentType
{
    TermsOfService = 0,
    PrivacyPolicy = 1,
    MarketingEmails = 2,
    DataProcessing = 3,
    CleanerFrameworkContract = 4,
    SelfBillingAgreement = 5,
    CleanerDataProcessingAgreement = 6
}
