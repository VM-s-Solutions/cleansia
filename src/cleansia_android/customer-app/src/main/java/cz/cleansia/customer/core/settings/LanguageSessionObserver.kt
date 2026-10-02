package cz.cleansia.customer.core.settings

import cz.cleansia.core.auth.TokenStore
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.filter
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Calls [LanguagePreferenceSync.reconcile] once per session start — the partner app's
 * `LanguageSessionObserver`, copied, and the twin of iOS `LanguageReconciler`.
 *
 * The **session becoming valid** is observed on [TokenStore.tokens] rather than called from each
 * sign-in path (password, Google, e-mail confirmation), the same reasoning
 * [cz.cleansia.core.notifications.PushTokenSessionObserver] was written for: a hook per path is a hook
 * to forget on the next one. A session already live when this attaches counts as one — a cold start
 * into a restored session is the launch most likely to be online after a push that failed. An edge,
 * not a level: `map { it != null }` does not re-fire on a token refresh.
 */
@Singleton
class LanguageSessionObserver @Inject constructor(
    private val tokenStore: TokenStore,
    private val languageSync: LanguagePreferenceSync,
) {
    fun attach(scope: CoroutineScope) {
        scope.launch {
            tokenStore.tokens
                .map { it != null }
                .distinctUntilChanged()
                .filter { it }
                .collect { languageSync.reconcile() }
        }
    }
}
