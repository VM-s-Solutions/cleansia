package cz.cleansia.customer.features.auth

import android.content.Context
import android.content.res.Resources
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.AuthRepository
import cz.cleansia.customer.core.auth.GoogleSignInController
import cz.cleansia.customer.core.settings.AppSettings
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * [AuthViewModel] error-surfacing rules.
 *
 * Two deliberately *different* policies live in this one class and this test
 * exists to keep them apart:
 *
 *  - **Registration** must show the server's own, already-translated message.
 *    "An account with this email already exists" is actionable; "Something
 *    went wrong" leaves the user re-typing a password that was never the
 *    problem. There is nothing to protect — the signup form tells you an email
 *    is taken by definition, or it cannot work.
 *  - **Forgot-password / resend-confirmation** must stay vague. A distinct
 *    "no account with that email" there turns the form into an account
 *    enumeration oracle, so those three siblings keep their purpose-specific
 *    fixed strings on purpose. The tests below pin that so a later "make the
 *    error handling consistent" pass has to argue with a red build first.
 *
 * The [Context] is a MockK stand-in for the real resource table; the
 * key → `R.string.error_*` transform itself is covered by `ApiErrorParserTest`.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class AuthViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var authRepository: AuthRepository
    private lateinit var settings: AppSettingsRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var googleSignInController: GoogleSignInController
    private lateinit var context: Context
    private lateinit var resources: Resources

    private val packageName = "cz.cleansia.customer"
    private val genericUnknown = "Something went wrong. Please try again."

    /** What `R.string.error_user_existing_email` renders as in the shipped en catalog. */
    private val existingEmailMessage = "An account with this email already exists."
    private val existingEmailResId = 4242

    @Before
    fun setUp() {
        authRepository = mockk(relaxed = true)
        settings = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        googleSignInController = mockk(relaxed = true)
        context = mockk(relaxed = true)
        resources = mockk(relaxed = true)

        // register() reads the language for the confirmation email before it calls the API.
        // A relaxed mock hands back a Flow that never emits, so first() would hang forever.
        every { settings.settings } returns flowOf(AppSettings())

        every { context.packageName } returns packageName
        every { context.resources } returns resources
        every { context.getString(R.string.error_generic_unknown) } returns genericUnknown
        every { resources.getIdentifier(any(), "string", packageName) } returns 0
        every {
            resources.getIdentifier("error_user_existing_email", "string", packageName)
        } returns existingEmailResId
        every { context.getString(existingEmailResId) } returns existingEmailMessage
    }

    private fun viewModel() = AuthViewModel(
        authRepository = authRepository,
        settings = settings,
        snackbar = snackbar,
        googleSignInController = googleSignInController,
        appContext = context,
    )

    private fun stubRegister(result: ApiResult<Unit>) {
        coEvery {
            authRepository.register(any(), any(), any(), any(), any(), any())
        } returns result
    }

    @Test
    fun `register rejected for a taken email shows the server's own message`() = runTest {
        // The backend answers 400 with errors { Email: "user.existing_email" }; safeApiCall
        // has already lifted that key onto ApiError.BadRequest.errorKey by the time it
        // reaches the VM. Discarding it here is what made every failed signup read
        // "Something went wrong".
        stubRegister(
            ApiResult.Error(
                ApiError.BadRequest(
                    message = "A validation problem occurred.",
                    errorKey = "user.existing_email",
                ),
            ),
        )

        val vm = viewModel()
        vm.register("taken@example.com", "Passw0rd!", "Ada", "Lovelace")
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showError(existingEmailMessage) }
        verify(exactly = 0) { snackbar.showErrorKey(R.string.error_generic_unknown) }
    }

    @Test
    fun `register failure clears loading and leaves the user on the form`() = runTest {
        stubRegister(
            ApiResult.Error(
                ApiError.BadRequest(
                    message = "A validation problem occurred.",
                    errorKey = "user.existing_email",
                ),
            ),
        )

        val vm = viewModel()
        vm.register("taken@example.com", "Passw0rd!", "Ada", "Lovelace")
        advanceUntilIdle()

        assertFalse(vm.uiState.value.loading)
        // No outcome — nothing must navigate away from a signup that did not happen.
        assertEquals(null, vm.uiState.value.outcome)
    }

    @Test
    fun `register with a transport failure falls back to the generic network message`() = runTest {
        // ApiError.Network carries an internal diagnostic string, never a user message —
        // the parser is what turns it into the localized "check your connection" line,
        // so routing through it must not start leaking raw exception text.
        every { context.getString(R.string.error_generic_network) } returns "Check your connection."
        stubRegister(ApiResult.Error(ApiError.Network("failed to connect to 10.0.2.2")))

        val vm = viewModel()
        vm.register("new@example.com", "Passw0rd!", "Ada", "Lovelace")
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showError("Check your connection.") }
        verify(exactly = 0) { snackbar.showError("failed to connect to 10.0.2.2") }
    }

    @Test
    fun `register success routes to email confirmation with the submitted email`() = runTest {
        stubRegister(ApiResult.Success(Unit))

        val vm = viewModel()
        vm.register("new@example.com", "Passw0rd!", "Ada", "Lovelace")
        advanceUntilIdle()

        assertEquals(
            AuthOutcome.NeedsEmailConfirm("new@example.com"),
            vm.uiState.value.outcome,
        )
        verify(exactly = 0) { snackbar.showError(any<String>()) }
        verify(exactly = 0) { snackbar.showErrorKey(any()) }
    }

    // ─── Enumeration defence: these three must NOT adopt the parsed message ───

    @Test
    fun `requestPasswordChange stays vague about whether the email is registered`() = runTest {
        coEvery { authRepository.requestPasswordChange(any(), any()) } returns
            ApiResult.Error(
                ApiError.BadRequest(
                    message = "A validation problem occurred.",
                    errorKey = "user.not_existing_email",
                ),
            )

        val vm = viewModel()
        vm.requestPasswordChange("stranger@example.com")
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showErrorKey(R.string.forgot_send_failed) }
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `changePassword stays vague about why the code was rejected`() = runTest {
        coEvery { authRepository.changePassword(any(), any(), any()) } returns
            ApiResult.Error(
                ApiError.BadRequest(
                    message = "A validation problem occurred.",
                    errorKey = "user.not_existing_email",
                ),
            )

        val vm = viewModel()
        vm.changePassword("stranger@example.com", "123456", "Passw0rd!")
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showErrorKey(R.string.forgot_change_failed) }
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `resendConfirmationEmail stays vague about whether the account exists`() = runTest {
        coEvery { authRepository.resendConfirmationEmail(any(), any()) } returns
            ApiResult.Error(
                ApiError.BadRequest(
                    message = "A validation problem occurred.",
                    errorKey = "user.not_existing_email",
                ),
            )

        val vm = viewModel()
        vm.resendConfirmationEmail("stranger@example.com")
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showErrorKey(R.string.error_email_sending_failed) }
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }
}
