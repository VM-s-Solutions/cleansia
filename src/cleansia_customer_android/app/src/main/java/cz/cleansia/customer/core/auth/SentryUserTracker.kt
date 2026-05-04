package cz.cleansia.customer.core.auth

import io.sentry.Sentry
import io.sentry.protocol.User
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch

/**
 * Mirrors the current JWT's user id into Sentry's global scope so crash reports
 * carry a stable identifier across sessions. We intentionally do NOT set the
 * email or any PII — just the opaque user id from the `sub` claim.
 */
object SentryUserTracker {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)

    fun start(tokenStore: TokenStore) {
        scope.launch {
            tokenStore.tokens
                .map { it?.accessToken?.let(JwtDecoder::extractUserId) }
                .distinctUntilChanged()
                .collect { userId ->
                    if (userId == null) {
                        Sentry.setUser(null)
                    } else {
                        Sentry.setUser(User().apply { id = userId })
                    }
                }
        }
    }
}
