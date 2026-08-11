package cz.cleansia.partner.core.settings

import cz.cleansia.core.auth.TokenStore
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Before
import org.junit.Test

/**
 * The trigger half of the reconcile: *when* the seam is called. What it then does — and what it
 * refuses to do for a cleaner who never picked a language — is [LanguagePreferenceSyncTest].
 */
@OptIn(ExperimentalCoroutinesApi::class)
class LanguageSessionObserverTest {

    private lateinit var tokenStore: TokenStore
    private lateinit var languageSync: LanguagePreferenceSync
    private lateinit var tokens: MutableStateFlow<TokenStore.Tokens?>

    @Before
    fun setUp() {
        tokens = MutableStateFlow(null)
        tokenStore = mockk(relaxed = true)
        languageSync = mockk(relaxed = true)
        every { tokenStore.tokens } returns tokens
    }

    @Test
    fun `reconciles once when a sign-in produces a session`() = runTest {
        observe()
        advanceUntilIdle()

        tokens.value = someTokens()
        advanceUntilIdle()

        verify(exactly = 1) { languageSync.reconcile() }
    }

    /**
     * Mirrors iOS `testASessionAlreadyLiveWhenTheReconcilerAttachesCountsAsOne`.
     *
     * A cold start into a restored session is included on purpose. The push fails because the
     * connection was dead at the moment of the tap; the next launch is minutes later and almost
     * certainly online, while the next sign-in may never come — so restricting this to fresh sign-ins
     * would leave the hole exactly where it was for the cleaner it exists for.
     */
    @Test
    fun `a session already live when the reconciler attaches counts as one`() = runTest {
        tokens.value = someTokens()

        observe()
        advanceUntilIdle()

        verify(exactly = 1) { languageSync.reconcile() }
    }

    @Test
    fun `does not fire before a session exists`() = runTest {
        observe()
        advanceUntilIdle()

        verify(exactly = 0) { languageSync.reconcile() }
    }

    @Test
    fun `sign-out adds no reconcile of its own`() = runTest {
        tokens.value = someTokens()
        observe()
        advanceUntilIdle()

        tokens.value = null
        advanceUntilIdle()

        verify(exactly = 1) { languageSync.reconcile() }
    }

    /** A token refresh replaces the value without ending the session, so it is not a new session. */
    @Test
    fun `does not fire again when the access token is refreshed mid-session`() = runTest {
        observe()
        advanceUntilIdle()
        tokens.value = someTokens()
        advanceUntilIdle()

        tokens.value = someTokens(access = "rotated")
        advanceUntilIdle()

        verify(exactly = 1) { languageSync.reconcile() }
    }

    @Test
    fun `reconciles again after a sign-out and a second sign-in`() = runTest {
        observe()
        advanceUntilIdle()

        tokens.value = someTokens()
        advanceUntilIdle()
        tokens.value = null
        advanceUntilIdle()
        tokens.value = someTokens()
        advanceUntilIdle()

        verify(exactly = 2) { languageSync.reconcile() }
    }

    @Test
    fun `stops observing when the attached scope dies`() = runTest {
        val scope = CoroutineScope(StandardTestDispatcher(testScheduler))
        LanguageSessionObserver(tokenStore, languageSync).attach(scope)
        advanceUntilIdle()

        scope.cancel()
        tokens.value = someTokens()
        advanceUntilIdle()

        verify(exactly = 0) { languageSync.reconcile() }
    }

    private fun TestScope.observe() =
        LanguageSessionObserver(tokenStore, languageSync)
            .attach(CoroutineScope(StandardTestDispatcher(testScheduler)))

    private fun someTokens(access: String = "access") = TokenStore.Tokens(
        accessToken = access,
        accessTokenExpiresAt = Long.MAX_VALUE,
        refreshToken = "refresh",
        refreshTokenExpiresAt = Long.MAX_VALUE,
    )
}
