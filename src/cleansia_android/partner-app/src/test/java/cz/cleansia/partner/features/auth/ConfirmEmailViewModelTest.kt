package cz.cleansia.partner.features.auth

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.core.auth.UserProfileData
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.settings.AppSettings
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.auth.LoginOutcome
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class ConfirmEmailViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var authRepository: AuthRepository
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var userProfileStore: UserProfileStore
    private lateinit var appSettingsRepository: AppSettingsRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var context: Context

    @Before
    fun setUp() {
        authRepository = mockk()
        errorTranslator = mockk()
        userProfileStore = mockk()
        appSettingsRepository = mockk()
        snackbar = mockk(relaxed = true)
        context = mockk()
        every { errorTranslator.translate(any()) } returns "translated error"
        every { context.getString(any()) } returns "generic error"
        coEvery { userProfileStore.current() } returns profile()
    }

    private fun profile(email: String = "a@b.com") = UserProfileData(
        userId = "user-1",
        email = email,
        employeeId = null,
        isEmailConfirmed = false,
        hasAdminAccess = false,
        firstName = null,
        lastName = null,
        role = null,
    )

    private fun viewModel(routeEmail: String? = null) = ConfirmEmailViewModel(
        SavedStateHandle(if (routeEmail == null) emptyMap() else mapOf("email" to routeEmail)),
        authRepository,
        errorTranslator,
        userProfileStore,
        appSettingsRepository,
        snackbar,
        context,
    )

    @Test
    fun `stored profile email is loaded into state`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        assertEquals("a@b.com", vm.uiState.value.email)
    }

    /**
     * The whole point of item #13: a partner who has just registered has no
     * session and no stored profile, so the address can only come from the
     * route. Before the fix, registration bounced back to Login precisely
     * because ConfirmEmail could not be reached without a profile — landing
     * here with an empty store used to leave the subtitle blank and make the
     * confirm call unresolvable.
     */
    @Test
    fun `route email is used when the profile store is empty`() = runTest {
        coEvery { userProfileStore.current() } returns null

        val vm = viewModel(routeEmail = "fresh@registrant.cz")
        advanceUntilIdle()

        assertEquals("fresh@registrant.cz", vm.uiState.value.email)
    }

    /**
     * The route argument is the primary source; the store is only a fallback.
     * They can legitimately disagree — a stale profile from a previous session
     * on a shared handset — and the address the user just typed must win.
     */
    @Test
    fun `route email wins over a stale stored profile`() = runTest {
        coEvery { userProfileStore.current() } returns profile(email = "stale@old.cz")

        val vm = viewModel(routeEmail = "fresh@registrant.cz")
        advanceUntilIdle()

        assertEquals("fresh@registrant.cz", vm.uiState.value.email)
    }

    /** A blank arg is not an address — fall through to the store. */
    @Test
    fun `blank route email falls back to the stored profile`() = runTest {
        val vm = viewModel(routeEmail = "")
        advanceUntilIdle()

        assertEquals("a@b.com", vm.uiState.value.email)
    }

    @Test
    fun `successful confirmation sends the stored email with the code and flags success`() = runTest {
        coEvery { authRepository.confirmEmail("a@b.com", "123456") } returns
            ApiResult.Success(LoginOutcome.Authenticated)

        val vm = viewModel()
        advanceUntilIdle()
        vm.onCodeChange("123456")
        vm.confirmEmail()
        advanceUntilIdle()

        coVerify { authRepository.confirmEmail("a@b.com", "123456") }
        assertTrue(vm.uiState.value.isConfirmationSuccessful)
        assertFalse(vm.uiState.value.isLoading)
    }

    @Test
    fun `confirmation failure snackbars and does not flag success`() = runTest {
        coEvery { authRepository.confirmEmail("a@b.com", "123456") } returns
            ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()
        vm.onCodeChange("123456")
        vm.confirmEmail()
        advanceUntilIdle()

        verify { snackbar.showError("translated error") }
        assertFalse(vm.uiState.value.isConfirmationSuccessful)
        assertFalse(vm.uiState.value.isLoading)
    }

    @Test
    fun `missing email surfaces an error and does not submit`() = runTest {
        coEvery { userProfileStore.current() } returns null

        val vm = viewModel()
        advanceUntilIdle()
        vm.onCodeChange("123456")
        vm.confirmEmail()
        advanceUntilIdle()

        coVerify(exactly = 0) { authRepository.confirmEmail(any(), any()) }
        assertNotNull(vm.uiState.value.error)
    }

    @Test
    fun `incomplete code does not submit`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        vm.onCodeChange("123")
        vm.confirmEmail()
        advanceUntilIdle()

        coVerify(exactly = 0) { authRepository.confirmEmail(any(), any()) }
    }

    /**
     * `resendCode` used to read `settings.first().language.tag ?: "en"`, and
     * `LanguagePreference.System` — the fresh-install default — carries a null
     * tag, so every re-sent confirmation email arrived in English no matter what
     * the handset was set to. Resolution belongs to
     * [AppSettingsRepository.emailLanguageTag]; this pins that the VM asks for it.
     */
    @Test
    fun `resendCode sends the resolved device language, not a hardcoded en`() = runTest {
        // A System preference really is sitting in DataStore — that is the default —
        // and it must still not be what decides the email language.
        every { appSettingsRepository.settings } returns flowOf(AppSettings())
        coEvery { appSettingsRepository.emailLanguageTag() } returns "cs"
        coEvery { authRepository.resendConfirmation(any(), any()) } returns
            ApiResult.Success(true)

        val vm = viewModel()
        advanceUntilIdle()
        vm.resendCode()
        advanceUntilIdle()

        coVerify(exactly = 1) { authRepository.resendConfirmation("a@b.com", "cs") }
    }
}
