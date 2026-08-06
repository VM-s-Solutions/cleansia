package cz.cleansia.core.consent

/**
 * The GDPR consent kinds the backend records, by their on-the-wire integer
 * (`Cleansia.Core.Domain.Enums.ConsentType`). Each app maps this onto its own
 * OpenAPI-generated enum, whose entries are named `_0`.. `_3`.
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
 * One tick, two records: both signup sentences name the Terms of Service and the
 * Privacy Policy by title. Neither form offers a marketing box, so
 * [SignupConsentType.MarketingEmails] must never appear here — a record nobody
 * ticked is worse than no record at all.
 */
val SIGNUP_TICK_CONSENTS: List<SignupConsentType> =
    listOf(SignupConsentType.TermsOfService, SignupConsentType.PrivacyPolicy)

/** A signup tick waiting for a session it can be recorded against. */
data class PendingSignupConsent(
    val email: String,
    val types: List<SignupConsentType>,
)
