package cz.cleansia.partner.core.settings

import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.user.CurrentUser
import cz.cleansia.partner.data.user.UserRepository
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.CoroutineExceptionHandler
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test

/**
 * `User.PreferredLanguageCode` is what `PayPeriodBackgroundService` renders the period-closed mail
 * AND the payout invoice PDF in — a tax document the cleaner files — so a language that only ever
 * reached DataStore left the app in Czech and the document that matters in whatever signup sent.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class LanguagePreferenceSyncTest {

    private lateinit var userRepository: UserRepository
    private lateinit var tokenStore: TokenStore

    @Before
    fun setUp() {
        userRepository = mockk(relaxed = true)
        tokenStore = mockk(relaxed = true)
        every { tokenStore.current() } returns tokens()
        coEvery { userRepository.getCurrentUser() } returns ApiResult.Success(profile())
        coEvery { userRepository.updateCurrentUser(any(), any(), any(), any(), any()) } returns
            ApiResult.Success(Unit)
    }

    // region the pure decision

    @Test
    fun `builds a full profile replay carrying the new language`() {
        val push = LanguagePreferencePush.forUser(profile(language = "en"), "uk")!!

        assertEquals("uk", push.languageCode)
        assertEquals("Ondrej", push.firstName)
        assertEquals("Novak", push.lastName)
        assertEquals("+420777111222", push.phoneNumber)
        assertEquals("1982-09-04", push.birthDate)
    }

    @Test
    fun `no push when the server already holds that language`() {
        assertNull(LanguagePreferencePush.forUser(profile(language = "uk"), "uk"))
    }

    @Test
    fun `no push when signed out`() {
        assertNull(LanguagePreferencePush.forUser(null, "uk"))
    }

    /**
     * First and last name are a blind replace on the handler and their validators reject blanks, so
     * replaying a nameless profile would 400 rather than save the language.
     */
    @Test
    fun `no push when the profile has no name to replay`() {
        assertNull(LanguagePreferencePush.forUser(profile(firstName = ""), "uk"))
        assertNull(LanguagePreferencePush.forUser(profile(lastName = ""), "uk"))
        assertNull(LanguagePreferencePush.forUser(profile(firstName = null), "uk"))
        assertNull(LanguagePreferencePush.forUser(profile(lastName = null), "uk"))
    }

    /**
     * A cleaner waiting on approval may not have filled a phone or a birth date yet, and that is the
     * population the registration lock's chooser exists for. Both are "nothing to say" fields the
     * handler preserves, so an absent one must not cost them the push — but the phone still has to
     * reach the wire as a blank string, because the command's `PhoneNumber` is a non-nullable
     * reference type and the host would refuse an omitted member outright.
     */
    @Test
    fun `an incomplete profile still pushes, replaying blanks rather than dropping the language`() {
        val push = LanguagePreferencePush.forUser(profile(phone = null, birthDate = null), "uk")!!

        assertEquals("uk", push.languageCode)
        assertEquals("", push.phoneNumber)
        assertNull(push.birthDate)
    }

    @Test
    fun `pushes when the server has no language yet`() {
        assertEquals("cs", LanguagePreferencePush.forUser(profile(language = null), "cs")?.languageCode)
    }

    // endregion

    // region the live sync

    @Test
    fun `live sync replays the whole profile alongside the new language`() = runTest {
        coEvery { userRepository.getCurrentUser() } returns ApiResult.Success(profile(language = "en"))

        sync().send("uk")
        advanceUntilIdle()

        coVerify(exactly = 1) {
            userRepository.updateCurrentUser(
                firstName = "Ondrej",
                lastName = "Novak",
                phoneNumber = "+420777111222",
                birthDate = "1982-09-04",
                languageCode = "uk",
            )
        }
    }

    @Test
    fun `live sync sends nothing when there is no session`() = runTest {
        every { tokenStore.current() } returns null

        sync().send("uk")
        advanceUntilIdle()

        coVerify(exactly = 0) { userRepository.getCurrentUser() }
        coVerify(exactly = 0) { userRepository.updateCurrentUser(any(), any(), any(), any(), any()) }
    }

    @Test
    fun `live sync sends nothing when nothing changed`() = runTest {
        coEvery { userRepository.getCurrentUser() } returns ApiResult.Success(profile(language = "uk"))

        sync().send("uk")
        advanceUntilIdle()

        coVerify(exactly = 0) { userRepository.updateCurrentUser(any(), any(), any(), any(), any()) }
    }

    @Test
    fun `live sync sends nothing when the profile cannot be read`() = runTest {
        coEvery { userRepository.getCurrentUser() } returns
            ApiResult.Error(ApiError.Network("offline"))

        sync().send("uk")
        advanceUntilIdle()

        coVerify(exactly = 0) { userRepository.updateCurrentUser(any(), any(), any(), any(), any()) }
    }

    /**
     * A display-language tap is not a save anyone is waiting on — a rejected push must not throw.
     * [pushFailures] records anything that escapes the seam's own scope.
     */
    @Test
    fun `live sync swallows a failed push`() = runTest {
        coEvery { userRepository.updateCurrentUser(any(), any(), any(), any(), any()) } returns
            ApiResult.Error(ApiError.BadRequest("nope"))

        sync().send("uk")
        advanceUntilIdle()

        coVerify(exactly = 1) { userRepository.updateCurrentUser(any(), any(), any(), any(), any()) }
        assertEquals(emptyList<Throwable>(), pushFailures)
    }

    /**
     * Why the seam owns a scope instead of running in the caller's: the settings picker pops the back
     * stack in the same tap, which clears the entry's ViewModelStore and cancels `viewModelScope`
     * somewhere between the profile read and the write.
     */
    @Test
    fun `a push owned by the calling screen dies with it`() = runTest {
        coEvery { userRepository.getCurrentUser() } returns ApiResult.Success(profile(language = "en"))
        val screenScope = CoroutineScope(StandardTestDispatcher(testScheduler))

        sync(screenScope).send("uk")
        screenScope.cancel()
        advanceUntilIdle()

        coVerify(exactly = 0) { userRepository.updateCurrentUser(any(), any(), any(), any(), any()) }

        sync().send("uk")
        advanceUntilIdle()

        coVerify(exactly = 1) { userRepository.updateCurrentUser(any(), any(), any(), any(), any()) }
    }

    // endregion

    private val pushFailures = mutableListOf<Throwable>()

    /**
     * Stands in for the injected `@ApplicationScope`: shares the test's scheduler so `advanceUntilIdle`
     * drives it, and records anything the seam lets escape.
     */
    private fun TestScope.sync(scope: CoroutineScope = syncScope()) =
        LiveLanguagePreferenceSync(tokenStore, userRepository, scope)

    private fun TestScope.syncScope() = CoroutineScope(
        StandardTestDispatcher(testScheduler) + SupervisorJob() +
            CoroutineExceptionHandler { _, throwable -> pushFailures += throwable },
    )

    private fun tokens() = TokenStore.Tokens(
        accessToken = "access",
        accessTokenExpiresAt = Long.MAX_VALUE,
        refreshToken = "refresh",
        refreshTokenExpiresAt = Long.MAX_VALUE,
    )

    private fun profile(
        firstName: String? = "Ondrej",
        lastName: String? = "Novak",
        phone: String? = "+420777111222",
        birthDate: String? = "1982-09-04",
        language: String? = "en",
    ) = CurrentUser(
        email = "ondrej@example.com",
        firstName = firstName,
        lastName = lastName,
        phoneNumber = phone,
        birthDate = birthDate,
        preferredLanguageCode = language,
    )
}
