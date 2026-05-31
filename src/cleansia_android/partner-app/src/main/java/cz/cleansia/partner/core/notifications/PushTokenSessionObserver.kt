package cz.cleansia.partner.core.notifications

import cz.cleansia.core.auth.TokenStore
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Long-term fix for "device wasn't registered" bugs.
 *
 * Previously, registration was scattered across three discrete event
 * hooks — login, email-confirm, and FCM token rotation — each of which
 * could be missed (cold launch on existing session never re-registers;
 * rotation while signed out silently 401s; users installed before FCM
 * was added never registered at all). Adding a fourth event hook would
 * just patch the next symptom.
 *
 * This observer makes registration a property of the session state
 * rather than an event: combine the auth-token flow with the FCM-token
 * flow, drop emissions where either is null, dedupe, and call
 * [PushTokenRepository.ensureRegistered] on every distinct pair.
 *
 * Behavioural properties it guarantees:
 *  - Cold start with an existing session → first emission fires
 *    registration. (Fixes existing users who installed before FCM.)
 *  - Login / email-confirm → auth flow flips null→non-null → fires.
 *  - FCM token rotation while signed in → fcm flow changes → fires.
 *  - FCM token rotation while signed out → emission buffered until the
 *    auth flow becomes non-null, then fires. (Was previously dropped.)
 *  - Logout → auth flow flips non-null→null → filter drops, no
 *    re-register. (Explicit unregister still runs from AuthRepository.)
 *  - Token already registered → [ensureRegistered] short-circuits on
 *    the local DataStore cache. So calling on every start is free.
 *
 * Started exactly once from `MainActivity.onCreate` on the activity's
 * lifecycleScope; the singleton scope means the observer survives
 * configuration changes but stops when the activity is finished.
 */
@Singleton
class PushTokenSessionObserver @Inject constructor(
    private val tokenStore: TokenStore,
    private val pushTokenRepository: PushTokenRepository,
) {
    /**
     * Hook the observer to [scope] (typically `MainActivity.lifecycleScope`).
     * Also kicks off the one-shot FCM token fetch so the [PushTokenRepository.fcmToken]
     * flow has something to emit even if FCM never rotates.
     */
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
