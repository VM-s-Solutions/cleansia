package cz.cleansia.customer.core.consent

/**
 * The GDPR consent kinds the backend records, by their on-the-wire integer
 * (`Cleansia.Core.Domain.Enums.ConsentType`), named so the booking review step does not have to
 * read the generated enum's `_0`..`_3` entries.
 */
enum class SignupConsentType(val wireValue: Int) {
    TermsOfService(0),
    PrivacyPolicy(1),
    MarketingEmails(2),
    DataProcessing(3),
    ;

    companion object {
        fun fromWireValue(wireValue: Int): SignupConsentType? =
            entries.firstOrNull { it.wireValue == wireValue }
    }
}

/**
 * One tick, two records: the signup and booking sentences name the Terms of Service and the
 * Privacy Policy by title. Neither form offers a marketing box, so
 * [SignupConsentType.MarketingEmails] must never appear here.
 */
val SIGNUP_TICK_CONSENTS: List<SignupConsentType> =
    listOf(SignupConsentType.TermsOfService, SignupConsentType.PrivacyPolicy)
