package cz.cleansia.partner.features.auth

import android.content.Context
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.core.auth.UserProfileData
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.ApiErrorTranslator
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

    private fun viewModel() = ConfirmEmailViewModel(
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
}
