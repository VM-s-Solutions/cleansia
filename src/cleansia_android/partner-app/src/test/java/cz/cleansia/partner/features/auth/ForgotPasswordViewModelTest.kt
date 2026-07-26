package cz.cleansia.partner.features.auth

import android.content.Context
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.settings.AppSettings
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The language a partner's password-reset email is rendered in.
 *
 * Same defect as register and resend-confirmation: the address was read straight
 * off `settings.first().language.tag ?: "en"`, and the default
 * `LanguagePreference.System` carries a null tag, so a partner locked out of the
 * app received the reset instructions in a language they may not read.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class ForgotPasswordViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var authRepository: AuthRepository
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var appSettingsRepository: AppSettingsRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var context: Context

    @Before
    fun setUp() {
        authRepository = mockk()
        errorTranslator = mockk(relaxed = true)
        appSettingsRepository = mockk()
        snackbar = mockk(relaxed = true)
        context = mockk()
        every { context.getString(any()) } returns "validation message"
        // A System preference really is sitting in DataStore — that is the default —
        // and it must still not be what decides the email language.
        every { appSettingsRepository.settings } returns flowOf(AppSettings())
    }

    private fun viewModel() = ForgotPasswordViewModel(
        authRepository,
        errorTranslator,
        appSettingsRepository,
        snackbar,
        context,
    )

    @Test
    fun `requestPasswordReset sends the resolved device language, not a hardcoded en`() = runTest {
        coEvery { appSettingsRepository.emailLanguageTag() } returns "sk"
        coEvery { authRepository.forgotPassword(any(), any()) } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.onEmailChange("ada@example.com")
        vm.requestPasswordReset()
        advanceUntilIdle()

        coVerify(exactly = 1) { authRepository.forgotPassword("ada@example.com", "sk") }
        assertTrue(vm.uiState.value.isRequestSuccessful)
    }
}
