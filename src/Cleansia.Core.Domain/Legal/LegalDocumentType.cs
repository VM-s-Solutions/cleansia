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
    WorkContract = 2
}
