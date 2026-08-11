package cz.cleansia.partner.features.auth

import android.content.Context
import app.cash.turbine.test
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.core.network.ApiError
import cz.cleansia.partner.R
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.auth.LoginOutcome
import cz.cleansia.partner.features.auth.LoginViewModel
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class LoginViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var authRepository: AuthRepository
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController
    private lateinit var context: Context

    @Before
    fun setUp() {
        authRepository = mockk()
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
        // Distinct sentinels per resource: a hardcoded English literal coming
        // back cannot equal any of them, and neither can the wrong key.
        context = mockk()
        every { context.getString(R.string.error_email_required) } returns EMAIL_REQUIRED
        every { context.getString(R.string.error_email_invalid) } returns EMAIL_INVALID
        every { context.getString(R.string.error_password_required) } returns PASSWORD_REQUIRED
    }

    private fun viewModel() = LoginViewModel(authRepository, errorTranslator, snackbar, context)

    @Test
    fun `blank email sets field error and does not submit`() = runTest {
        val vm = viewModel()
        vm.onPasswordChange("secret")
        vm.login()
        advanceUntilIdle()

        assertEquals(EMAIL_REQUIRED, vm.uiState.value.emailError)
        assertEquals(ActionState.Idle, vm.loginState.value)
        coVerify(exactly = 0) { authRepository.login(any(), any(), any()) }
    }

    @Test
    fun `invalid email format sets field error and does not submit`() = runTest {
        val vm = viewModel()
        vm.onEmailChange("not-an-email")
        vm.onPasswordChange("secret")
        vm.login()
        advanceUntilIdle()

        assertEquals(EMAIL_INVALID, vm.uiState.value.emailError)
        coVerify(exactly = 0) { authRepository.login(any(), any(), any()) }
    }

    /**
     * The three validation messages were English literals on a screen a Czech,
     * Slovak, Ukrainian or Russian cleaner sees before anything else. The keys
     * existed and were translated in all five locales — they were simply never
     * read. Nothing failed, because an untranslated literal is invisible to
     * both `R` and a locale key-parity check.
     */
    @Test
    fun `every validation message comes from a string resource`() = runTest {
        val vm = viewModel()
        vm.login()
        advanceUntilIdle()

        assertEquals(EMAIL_REQUIRED, vm.uiState.value.emailError)
        assertEquals(PASSWORD_REQUIRED, vm.uiState.value.passwordError)
        verify { context.getString(R.string.error_email_required) }
        verify { context.getString(R.string.error_password_required) }
    }

    @Test
    fun `successful login emits success effect and returns to Idle`() = runTest {
        coEvery { authRepository.login("a@b.com", "secret", true) } returns
            ApiResult.Success(LoginOutcome.Authenticated)

        val vm = viewModel()
        vm.onEmailChange("a@b.com")
        vm.onPasswordChange("secret")

        vm.loginSuccess.test {
            vm.login()
            advanceUntilIdle()
            val effect = awaitItem()
            assertEquals(false, effect.requiresEmailConfirmation)
        }
        assertEquals(ActionState.Idle, vm.loginState.value)
    }

    @Test
    fun `unverified email login flags requiresEmailConfirmation in effect`() = runTest {
        coEvery { authRepository.login("a@b.com", "secret", true) } returns
            ApiResult.Success(LoginOutcome.UnverifiedEmail(email = "a@b.com", hasToken = false))

        val vm = viewModel()
        vm.onEmailChange("a@b.com")
        vm.onPasswordChange("secret")

        vm.loginSuccess.test {
            vm.login()
            advanceUntilIdle()
            assertEquals(true, awaitItem().requiresEmailConfirmation)
        }
    }

    /**
     * The address that rides to ConfirmEmail must be the one the *server*
     * says the code was issued to, not the one the user typed. The two differ
     * whenever the server normalises the address — here, case — and the
     * confirm call is validated against the server's copy, so sending the
     * typed one back would fail a code that is perfectly valid.
     */
    @Test
    fun `unverified login carries the server email, not the typed one`() = runTest {
        coEvery { authRepository.login("A@B.com", "secret", true) } returns
            ApiResult.Success(LoginOutcome.UnverifiedEmail(email = "a@b.com", hasToken = false))

        val vm = viewModel()
        vm.onEmailChange("A@B.com")
        vm.onPasswordChange("secret")

        vm.loginSuccess.test {
            vm.login()
            advanceUntilIdle()
            assertEquals("a@b.com", awaitItem().email)
        }
    }

    /** A verified login has no UnverifiedEmail to read — fall back to the typed address. */
    @Test
    fun `verified login falls back to the typed email`() = runTest {
        coEvery { authRepository.login("a@b.com", "secret", true) } returns
            ApiResult.Success(LoginOutcome.Authenticated)

        val vm = viewModel()
        vm.onEmailChange("a@b.com")
        vm.onPasswordChange("secret")

        vm.loginSuccess.test {
            vm.login()
            advanceUntilIdle()
            assertEquals("a@b.com", awaitItem().email)
        }
    }

    /**
     * One session lifetime for every mobile surface: 30 days, always requested.
     *
     * This is the only client in the product where remember-me was ever *functional* —
     * `MobilePartnerLogin` honours `command.RememberMe`, where `MobileLogin` (customer)
     * discards it and forces the long lifetime server-side. So a partner who unticked the
     * box really did get a 24-hour refresh token and really was signed out after a day of
     * not opening the app. The box is gone and the flag is pinned true.
     *
     * The field stays on the wire because the command declares it and the web keeps its own
     * checkbox — this is a client-side decision, not a server change.
     */
    @Test
    fun `login always requests the long-lived session`() = runTest {
        coEvery { authRepository.login(any(), any(), any()) } returns
            ApiResult.Success(LoginOutcome.Authenticated)

        val vm = viewModel()
        vm.onEmailChange("a@b.com")
        vm.onPasswordChange("secret")
        vm.login()
        advanceUntilIdle()

        coVerify(exactly = 1) { authRepository.login("a@b.com", "secret", true) }
    }

    private companion object {
        const val EMAIL_REQUIRED = "res:error_email_required"
        const val EMAIL_INVALID = "res:error_email_invalid"
        const val PASSWORD_REQUIRED = "res:error_password_required"
    }

    @Test
    fun `login failure snackbars and returns to Idle without success effect`() = runTest {
        coEvery { authRepository.login("a@b.com", "secret", true) } returns
            ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        vm.onEmailChange("a@b.com")
        vm.onPasswordChange("secret")
        vm.login()
        advanceUntilIdle()

        verify { snackbar.showError("translated error") }
        assertEquals(ActionState.Idle, vm.loginState.value)
    }
}
