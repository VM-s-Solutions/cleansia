package cz.cleansia.customer.core.notifications

import cz.cleansia.core.auth.TokenStore
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Long-term fix for "device wasn't registered" bugs (see partner-app
 * twin of this class for the full rationale).
 *
 * Combines the auth-session flow with the FCM-token flow, drops
 * emissions where either is null, and calls
 * [PushTokenRepository.ensureRegistered] on each distinct pair. This
 * replaces the previous scattered event hooks (login / signUp /
 * onNewToken), each of which could be silently missed.
 *
 * Attached once from `MainActivity.onCreate`.
 */
@Singleton
class PushTokenSessionObserver @Inject constructor(
    private val tokenStore: TokenStore,
    private val pushTokenRepository: PushTokenRepository,
) {
    fun attach(scope: CoroutineScope) {
        scope.launch { pushTokenRepository.fetchAndCacheCurrentToken() }

        scope.launch {
            combine(
                tokenStore.tokens,
                pushTokenRepository.fcmToken,
            ) { session, fcm -> if (session != null) fcm else null }
                .filterNotNull()
                .distinctUntilChanged()
                .collect { fcm -> pushTokenRepository.ensureRegistered(fcm) }
        }
    }
}
