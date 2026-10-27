package cz.cleansia.core.auth

import javax.inject.Inject
import javax.inject.Singleton

/**
 * The password just typed at registration, held in memory for the e-mail confirmation that follows, which
 * sets the account's password to the one it carries. When nothing is held for the address being
 * confirmed (the app restarted, or the registration was made elsewhere) the confirm screen asks for one.
 * Every session wipe forgets it, so it cannot outlive the confirmation or reach the next account.
 */
@Singleton
class RegistrationPassword @Inject constructor() : SessionScopedCache {

    @Volatile
    private var held: Held? = null

    fun hold(email: String, password: String) {
        held = Held(email.trim(), password)
    }

    fun heldFor(email: String): String? =
        held?.takeIf { it.email.equals(email.trim(), ignoreCase = true) }?.password

    override suspend fun clear() {
        held = null
    }

    private class Held(val email: String, val password: String)
}
