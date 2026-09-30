using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Legal;

[SwaggerEnumAsInt]
public enum LegalDocumentType
{
    TermsOfService = 0,
    PrivacyPolicy = 1,

    /// <summary>
    /// The contract for work between the customer and the cleaner, per job. A customer-audience text
    /// like the two above: the customer is bound at booking, so it is published where the customer's
    /// texts are and stamped on the order; the cleaner accepts that same text for their seat.
    /// </summary>
    WorkContract = 2,

    /// <summary>
    /// The three a cleaner accepts once and again on every new version: approval and every take are
    /// refused while one is in force for their market and its current version is not accepted.
    /// Employee-audience texts, recorded on the cleaner's consent rows.
    /// </summary>
    CleanerFrameworkContract = 3,
    SelfBillingAgreement = 4,
    CleanerDataProcessingAgreement = 5,

    /// <summary>The complaints procedure, a customer-audience text read, never accepted.</summary>
    ComplaintsProcedure = 6
}
