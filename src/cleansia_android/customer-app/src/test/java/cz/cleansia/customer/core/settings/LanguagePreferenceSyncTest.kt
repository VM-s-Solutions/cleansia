package cz.cleansia.customer.core.settings

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.user.CurrentUser
import cz.cleansia.customer.core.user.UserRepository
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.coVerifyOrder
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class LanguagePreferenceSyncTest {

    private lateinit var userRepository: UserRepository
    private lateinit var appSettingsRepository: AppSettingsRepository
    private val currentUser = MutableStateFlow<CurrentUser?>(null)
    private val chosen = MutableStateFlow(AppSettings())

    @Before
    fun setUp() {
        userRepository = mockk(relaxed = true)
        appSettingsRepository = mockk(relaxed = true)
        every { userRepository.currentUser } returns currentUser
        every { appSettingsRepository.settings } returns chosen
        coEvery { userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any()) } returns
            ApiResult.Success(Unit)
    }

    // region the pure decision

    @Test
    fun `builds a full profile replay carrying the new language`() {
        val push = LanguagePreferencePush.forUser(profile(language = "en"), "uk")!!

        assertEquals("uk", push.languageCode)
        assertEquals("Olena", push.firstName)
        assertEquals("Kovalenko", push.lastName)
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
     * First and last name are still a blind replace on the handler and their validators reject blanks,
     * so replaying a profile without them would 400 rather than save the language.
     */
    @Test
    fun `no push when a name is missing`() {
        assertNull(LanguagePreferencePush.forUser(profile(firstName = ""), "uk"))
        assertNull(LanguagePreferencePush.forUser(profile(lastName = ""), "uk"))
    }

    /**
     * The server validates a phone only when one is given and leaves the stored one alone on a blank,
     * so a customer without one — typically a Google or Apple sign-up — still gets their language synced.
     * The blank travels as "" because an omitted PhoneNumber is refused by the model binder.
     */
    @Test
    fun `no phone still pushes, with an empty phone`() {
        assertEquals("", LanguagePreferencePush.forUser(profile(phone = null), "uk")?.phoneNumber)
        assertEquals("", LanguagePreferencePush.forUser(profile(phone = "   "), "uk")?.phoneNumber)
        assertEquals("uk", LanguagePreferencePush.forUser(profile(phone = null), "uk")?.languageCode)
    }

    @Test
    fun `pushes when the server has no language yet`() {
        assertEquals("cs", LanguagePreferencePush.forUser(profile(language = null), "cs")?.languageCode)
    }

    // endregion

    // region the live sync

    @Test
    fun `live sync replays the whole profile alongside the new language`() = runTest {
        currentUser.value = profile(language = "en")

        sync().send("uk")

        coVerify(exactly = 1) {
            userRepository.updateCurrentUser(
                firstName = "Olena",
                lastName = "Kovalenko",
                phoneNumber = "+420777111222",
                birthDate = "1982-09-04",
                languageCode = "uk",
            )
        }
    }

    @Test
    fun `live sync sends nothing when signed out`() = runTest {
        sync().send("uk")

        coVerify(exactly = 0) { userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any()) }
    }

    @Test
    fun `live sync sends nothing when nothing changed`() = runTest {
        currentUser.value = profile(language = "uk")

        sync().send("uk")

        coVerify(exactly = 0) { userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any()) }
    }

    /** A display-language tap is not a save anyone is waiting on — a rejected push must not throw. */
    @Test
    fun `live sync swallows a failed push`() = runTest {
        currentUser.value = profile(language = "en")
        coEvery { userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any()) } returns
            ApiResult.Error(ApiError.BadRequest("nope"))

        sync().send("uk")
    }

    // region the session reconcile

    @Test
    fun `reconcile reads the server, then pushes the chosen language when it differs`() = runTest {
        chosen.value = AppSettings(language = LanguagePreference.Czech)
        coEvery { userRepository.refreshCurrentUser() } coAnswers {
            currentUser.value = profile(language = "en", phone = null)
            ApiResult.Success(Unit)
        }

        sync().reconcile()

        coVerifyOrder {
            userRepository.refreshCurrentUser()
            userRepository.updateCurrentUser(
                firstName = "Olena",
                lastName = "Kovalenko",
                phoneNumber = "",
                birthDate = "1982-09-04",
                languageCode = "cs",
            )
        }
    }

    @Test
    fun `reconcile writes nothing when the server already agrees`() = runTest {
        chosen.value = AppSettings(language = LanguagePreference.Czech)
        coEvery { userRepository.refreshCurrentUser() } coAnswers {
            currentUser.value = profile(language = "cs")
            ApiResult.Success(Unit)
        }

        sync().reconcile()

        coVerify(exactly = 0) { userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any()) }
    }

    /** "System" is the handset's locale ordering, not a choice — it must not overwrite another client's. */
    @Test
    fun `reconcile does nothing at all for a user who never chose a language`() = runTest {
        chosen.value = AppSettings(language = LanguagePreference.System)

        sync().reconcile()

        coVerify(exactly = 0) { userRepository.refreshCurrentUser() }
        coVerify(exactly = 0) { userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any()) }
    }

    // endregion

    private fun sync() = LiveLanguagePreferenceSync(userRepository, appSettingsRepository)

    private fun profile(
        firstName: String = "Olena",
        lastName: String = "Kovalenko",
        phone: String? = "+420777111222",
        language: String? = "en",
    ) = CurrentUser(
        id = "user-1",
        email = "olena@example.com",
        firstName = firstName,
        lastName = lastName,
        phoneNumber = phone,
        birthDate = "1982-09-04",
        preferredLanguageCode = language,
    )
}
