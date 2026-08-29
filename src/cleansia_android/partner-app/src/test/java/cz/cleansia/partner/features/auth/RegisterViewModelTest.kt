package cz.cleansia.partner.features.auth

import android.content.Context
import cz.cleansia.core.consent.SignupConsentRepository
import cz.cleansia.core.network.ApiError
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
import org.junit.Assert.assertNotNull
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The language a partner's confirmation email is rendered in.
 *
 * `register()` used to read `settings.first().language.tag ?: "en"`, and
 * `LanguagePreference.System` — the default on a fresh install, which is
 * precisely when someone registers — carries a null tag. So the fallback fired
 * for every new partner and the confirmation code arrived in English on a Czech,
 * Slovak, Ukrainian or Russian handset. Resolution now belongs to
 * [AppSettingsRepository.emailLanguageTag].
 */
@OptIn(ExperimentalCoroutinesApi::class)
class RegisterViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var authRepository: AuthRepository
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var appSettingsRepository: AppSettingsRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var signupConsent: SignupConsentRepository
    private lateinit var context: Context

    @Before
    fun setUp() {
        authRepository = mockk()
        errorTranslator = mockk(relaxed = true)
        appSettingsRepository = mockk()
        snackbar = mockk(relaxed = true)
        signupConsent = mockk(relaxed = true)
        context = mockk()
        every { context.getString(any()) } returns "validation message"
        // A System preference really is sitting in DataStore — that is the default —
        // and it must still not be what decides the email language.
        every { appSettingsRepository.settings } returns flowOf(AppSettings())
    }

    private fun viewModel() = RegisterViewModel(
        authRepository,
        errorTranslator,
        appSettingsRepository,
        snackbar,
        signupConsent,
        context,
    )

    /** Fills in a form that clears every validation branch in `register()`. */
    private fun RegisterViewModel.fillValidForm() {
        onFirstNameChange("Ada")
        onLastNameChange("Lovelace")
        onEmailChange("ada@example.com")
        onPasswordChange("Passw0rd")
        onConfirmPasswordChange("Passw0rd")
        onAcceptTermsChange(true)
    }

    @Test
    fun `register sends the resolved device language, not a hardcoded en`() = runTest {
        coEvery { appSettingsRepository.emailLanguageTag() } returns "cs"
        coEvery { authRepository.register(any(), any(), any(), any(), any()) } returns
            ApiResult.Success(Unit)

        val vm = viewModel()
        vm.fillValidForm()
        vm.register()
        advanceUntilIdle()

        coVerify(exactly = 1) {
            authRepository.register(
                email = "ada@example.com",
                password = "Passw0rd",
                firstName = "Ada",
                lastName = "Lovelace",
                language = "cs",
            )
        }
    }

    /**
     * An explicit picker choice still wins — the resolver, not the ViewModel,
     * decides that, so all this pins is that whatever it returns is what ships.
     */
    @Test
    fun `register forwards whatever the resolver returns`() = runTest {
        coEvery { appSettingsRepository.emailLanguageTag() } returns "uk"
        coEvery { authRepository.register(any(), any(), any(), any(), any()) } returns
            ApiResult.Success(Unit)

        val vm = viewModel()
        vm.fillValidForm()
        vm.register()
        advanceUntilIdle()

        coVerify(exactly = 1) {
            authRepository.register(any(), any(), any(), any(), language = "uk")
        }
    }

    /**
     * The terms box is a hard blocker, not a hint. It is the reason the "unticked box
     * records nothing" rule in [cz.cleansia.core.consent.SignupConsentRepository] can never
     * fire from this screen — and the reason that rule cannot be the only thing pinning it.
     */
    @Test
    fun `an unticked terms box does not register at all`() = runTest {
        coEvery { appSettingsRepository.emailLanguageTag() } returns "cs"
        coEvery { authRepository.register(any(), any(), any(), any(), any()) } returns
            ApiResult.Success(Unit)

        val vm = viewModel()
        vm.fillValidForm()
        vm.onAcceptTermsChange(false)
        vm.register()
        advanceUntilIdle()

        coVerify(exactly = 0) { authRepository.register(any(), any(), any(), any(), any()) }
        coVerify(exactly = 0) { signupConsent.recordSignupTick(any(), any()) }
        assertNotNull(vm.uiState.value.termsError)
    }

    @Test
    fun `a successful registration records the tick against the submitted address`() = runTest {
        coEvery { appSettingsRepository.emailLanguageTag() } returns "cs"
        coEvery { authRepository.register(any(), any(), any(), any(), any()) } returns
            ApiResult.Success(Unit)

        val vm = viewModel()
        vm.fillValidForm()
        vm.register()
        advanceUntilIdle()

        coVerify(exactly = 1) { signupConsent.recordSignupTick("ada@example.com", true) }
    }

    @Test
    fun `a rejected registration records nothing`() = runTest {
        coEvery { appSettingsRepository.emailLanguageTag() } returns "cs"
        coEvery { authRepository.register(any(), any(), any(), any(), any()) } returns
            ApiResult.Error(ApiError.BadRequest(message = "taken", errorKey = "user.existing_email"))

        val vm = viewModel()
        vm.fillValidForm()
        vm.register()
        advanceUntilIdle()

        coVerify(exactly = 0) { signupConsent.recordSignupTick(any(), any()) }
    }
}
