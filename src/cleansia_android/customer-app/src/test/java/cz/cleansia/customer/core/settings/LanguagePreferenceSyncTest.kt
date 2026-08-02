package cz.cleansia.customer.core.settings

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.user.CurrentUser
import cz.cleansia.customer.core.user.UserRepository
import io.mockk.coEvery
import io.mockk.coVerify
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
    private val currentUser = MutableStateFlow<CurrentUser?>(null)

    @Before
    fun setUp() {
        userRepository = mockk(relaxed = true)
        every { userRepository.currentUser } returns currentUser
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
     * so replaying an incomplete profile would 400 rather than save the language.
     */
    @Test
    fun `no push when the profile is incomplete`() {
        assertNull(LanguagePreferencePush.forUser(profile(firstName = ""), "uk"))
        assertNull(LanguagePreferencePush.forUser(profile(lastName = ""), "uk"))
        assertNull(LanguagePreferencePush.forUser(profile(phone = null), "uk"))
        assertNull(LanguagePreferencePush.forUser(profile(phone = "   "), "uk"))
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

        LiveLanguagePreferenceSync(userRepository).send("uk")

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
        LiveLanguagePreferenceSync(userRepository).send("uk")

        coVerify(exactly = 0) { userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any()) }
    }

    @Test
    fun `live sync sends nothing when nothing changed`() = runTest {
        currentUser.value = profile(language = "uk")

        LiveLanguagePreferenceSync(userRepository).send("uk")

        coVerify(exactly = 0) { userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any()) }
    }

    /** A display-language tap is not a save anyone is waiting on — a rejected push must not throw. */
    @Test
    fun `live sync swallows a failed push`() = runTest {
        currentUser.value = profile(language = "en")
        coEvery { userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any()) } returns
            ApiResult.Error(ApiError.BadRequest("nope"))

        LiveLanguagePreferenceSync(userRepository).send("uk")
    }

    // endregion

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
