package cz.cleansia.partner.features.auth

import app.cash.turbine.test
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.core.network.ApiError
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.auth.LoginOutcome
import cz.cleansia.partner.features.auth.viewmodels.LoginViewModel
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
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
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

    @Before
    fun setUp() {
        authRepository = mockk()
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
    }

    private fun viewModel() = LoginViewModel(authRepository, errorTranslator, snackbar)

    @Test
    fun `blank email sets field error and does not submit`() = runTest {
        val vm = viewModel()
        vm.onPasswordChange("secret")
        vm.login()
        advanceUntilIdle()

        assertNotNull(vm.uiState.value.emailError)
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

        assertNotNull(vm.uiState.value.emailError)
        coVerify(exactly = 0) { authRepository.login(any(), any(), any()) }
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
