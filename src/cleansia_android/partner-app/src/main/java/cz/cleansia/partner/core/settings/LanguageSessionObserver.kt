package cz.cleansia.partner.core.settings

import cz.cleansia.core.auth.TokenStore
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.filter
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Closes the one hole the three language pickers leave: [LanguagePreferenceSync.send] is silent on
 * failure and never retried, so a push lost to a dead connection heals only when the cleaner next
 * opens a picker — which they have no reason to do, because the app already shows the language they
 * asked for. The period-closed email and the payout invoice PDF keep arriving in the old one.
 *
 * Where "after sign-in" is: the **session becoming valid**, observed on [TokenStore.tokens] rather
 * than called from the login paths. There are two of those — `login` and `confirmEmail` — and the
 * same reasoning [cz.cleansia.core.notifications.PushTokenSessionObserver] was written for applies:
 * a hook per completion path is a hook to forget on the third one. Observing the store also lands
 * strictly after the token is persisted, which the push needs in order to carry a bearer.
 *
 * A session that is already live when this attaches counts as one. [TokenStore.tokens] is a
 * `StateFlow` seeded from prefs, so a cold start into a restored session replays a non-null token
 * that is not, strictly, a sign-in — and including it is the whole point. The push fails because the
 * connection was dead at the moment of the tap; the next **launch** is minutes later and almost
 * certainly online, while the next **sign-in** may never come. Restricting this to fresh sign-ins
 * would leave the hole exactly where it was for the cleaner it exists for: `map { it != null }` does
 * not re-fire on a token refresh, so someone signed in for months would never reach a second edge.
 *
 * The round trip that buys is small and lands only on cleaners who used a picker:
 * [LiveLanguagePreferenceSync.reconcile] returns on an unset preference *before* it reads the
 * profile, so a cleaner who never chose a language costs nothing at all.
 *
 * It never fires pre-session: the filter passes only a non-null token, and
 * [LiveLanguagePreferenceSync] re-checks the store before touching the network.
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
