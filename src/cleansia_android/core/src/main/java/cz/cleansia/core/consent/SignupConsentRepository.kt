package cz.cleansia.core.consent

import android.util.Log
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.CancellationException

/**
 * Carries the consent ticked at signup to the first session that can record it.
 *
 * Neither app's registration issues a session — both return a bare boolean and route to
 * email confirmation — while `GrantConsent` needs an authenticated caller. So the tick is
 * parked against the address it was ticked for and delivered only once the server itself
 * has named that same account in a token response.
 *
 * Every path here is best-effort and swallowing: a refusal, a 5xx or airplane mode must
 * leave signup and sign-in working. Whatever did not land stays parked and is retried at
 * the next session, so nothing is ever surfaced as a signup error.
 */
@Singleton
class SignupConsentRepository @Inject constructor(
    private val store: SignupConsentStore,
    private val client: SignupConsentClient,
) {

    suspend fun recordSignupTick(email: String, accepted: Boolean) {
        if (!accepted) return
        val normalized = normalizeEmail(email)
        if (normalized.isEmpty()) return
        bestEffort { store.save(PendingSignupConsent(normalized, SIGNUP_TICK_CONSENTS)) }
    }

    /**
     * @param sessionEmail the address the SERVER named in its token response. Never a form
     * field: the form is what an unauthenticated caller typed, so matching a parked tick on
     * it would let one account's session record another account's consent.
     */
    suspend fun deliverFor(sessionEmail: String?) {
        bestEffort {
            val pending = store.read() ?: return@bestEffort
            if (sessionEmail == null || normalizeEmail(sessionEmail) != pending.email) return@bestEffort

            val answered = client.answeredTypes() ?: return@bestEffort
            pending.types.forEach { type ->
                // Dropped rather than re-granted when the account has already answered:
                // otherwise a delayed retry resurrects a consent since withdrawn.
                val delivered = type in answered || client.grant(type) != ConsentGrantOutcome.Failed
                if (delivered) store.settle(type)
            }
        }
    }

    private inline fun bestEffort(block: () -> Unit) {
        try {
            block()
        } catch (ce: CancellationException) {
            throw ce
        } catch (t: Throwable) {
            Log.w(TAG, "Signup consent delivery failed: ${t.message}")
        }
    }

    private companion object {
        const val TAG = "SignupConsent"
    }
}

private fun normalizeEmail(email: String): String = email.trim().lowercase()
