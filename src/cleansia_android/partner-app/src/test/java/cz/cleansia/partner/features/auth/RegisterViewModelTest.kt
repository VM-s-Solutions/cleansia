package cz.cleansia.partner.features.auth

import android.content.Context
import cz.cleansia.core.consent.SignupConsentRepository
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.core.market.MarketListItem
import cz.cleansia.partner.core.market.MarketRepository
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
import org.junit.Assert.assertEquals
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
    private lateinit var marketRepository: MarketRepository
    private lateinit var context: Context

    private val cze = market("cze-id", "CZE", "Czech Republic", isDefault = true)
    private val svk = market("svk-id", "SVK", "Slovakia")

    @Before
    fun setUp() {
        authRepository = mockk()
        errorTranslator = mockk(relaxed = true)
        appSettingsRepository = mockk()
        snackbar = mockk(relaxed = true)
        signupConsent = mockk(relaxed = true)
        marketRepository = mockk()
        context = mockk()
        every { context.getString(any()) } returns "validation message"
        coEvery { marketRepository.getMarkets() } returns ApiResult.Success(listOf(svk, cze))
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
        marketRepository,
        context,
    )

    private fun market(countryId: String, iso: String, name: String, isDefault: Boolean = false) =
        MarketListItem(countryId = countryId, isoCode = iso, isoAlpha2 = iso.take(2), name = name, isDefault = isDefault)

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
        coEvery { authRepository.register(any(), any(), any(), any(), any(), any()) } returns
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
        coEvery { authRepository.register(any(), any(), any(), any(), any(), any()) } returns
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
        coEvery { authRepository.register(any(), any(), any(), any(), any(), any()) } returns
            ApiResult.Success(Unit)

        val vm = viewModel()
        vm.fillValidForm()
        vm.onAcceptTermsChange(false)
        vm.register()
        advanceUntilIdle()

        coVerify(exactly = 0) { authRepository.register(any(), any(), any(), any(), any(), any()) }
        coVerify(exactly = 0) { signupConsent.recordSignupTick(any(), any()) }
        assertNotNull(vm.uiState.value.termsError)
    }

    @Test
    fun `a successful registration records the tick against the submitted address`() = runTest {
        coEvery { appSettingsRepository.emailLanguageTag() } returns "cs"
        coEvery { authRepository.register(any(), any(), any(), any(), any(), any()) } returns
            ApiResult.Success(Unit)

        val vm = viewModel()
        vm.fillValidForm()
        vm.register()
        advanceUntilIdle()

        coVerify(exactly = 1) { signupConsent.recordSignupTick("ada@example.com", true) }
    }

    // ── the market picker (ADR-0061 D3/D6) ──

    /**
     * The picker reads `Market/GetOverview`, not `Country/GetServiced`: the latter still lists a
     * serviced country nobody operates, which would fail `tenant.not_found` on submit.
     */
    @Test
    fun `the picker lists what the partner host returns and preselects the default market`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(listOf(svk, cze), vm.uiState.value.markets)
        assertEquals("cze-id", vm.uiState.value.selectedMarketId)
    }

    @Test
    fun `with no market flagged as default the first listed one is preselected`() = runTest {
        val deu = market("deu-id", "DEU", "Germany")
        coEvery { marketRepository.getMarkets() } returns ApiResult.Success(listOf(deu, svk))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals("deu-id", vm.uiState.value.selectedMarketId)
    }

    @Test
    fun `register sends the preselected market`() = runTest {
        coEvery { appSettingsRepository.emailLanguageTag() } returns "cs"
        coEvery { authRepository.register(any(), any(), any(), any(), any(), any()) } returns
            ApiResult.Success(Unit)

        val vm = viewModel()
        advanceUntilIdle()
        vm.fillValidForm()
        vm.register()
        advanceUntilIdle()

        coVerify(exactly = 1) {
            authRepository.register(any(), any(), any(), any(), any(), countryId = "cze-id")
        }
    }

    @Test
    fun `register sends the market the cleaner picked`() = runTest {
        coEvery { appSettingsRepository.emailLanguageTag() } returns "cs"
        coEvery { authRepository.register(any(), any(), any(), any(), any(), any()) } returns
            ApiResult.Success(Unit)

        val vm = viewModel()
        advanceUntilIdle()
        vm.fillValidForm()
        vm.onMarketChange("svk-id")
        vm.register()
        advanceUntilIdle()

        coVerify(exactly = 1) {
            authRepository.register(any(), any(), any(), any(), any(), countryId = "svk-id")
        }
    }

    /**
     * A directory that cannot be read leaves the picker empty and the request without a market — the
     * server then scopes the account to the default market, which is what every registration did
     * before the picker existed.
     */
    @Test
    fun `a directory that cannot be read registers with no market rather than blocking`() = runTest {
        coEvery { marketRepository.getMarkets() } returns
            ApiResult.Error(ApiError.Network(message = "offline"))
        coEvery { appSettingsRepository.emailLanguageTag() } returns "cs"
        coEvery { authRepository.register(any(), any(), any(), any(), any(), any()) } returns
            ApiResult.Success(Unit)

        val vm = viewModel()
        advanceUntilIdle()
        vm.fillValidForm()
        vm.register()
        advanceUntilIdle()

        assertEquals(emptyList<MarketListItem>(), vm.uiState.value.markets)
        coVerify(exactly = 1) {
            authRepository.register(any(), any(), any(), any(), any(), countryId = null)
        }
    }

    @Test
    fun `a rejected registration records nothing`() = runTest {
        coEvery { appSettingsRepository.emailLanguageTag() } returns "cs"
        coEvery { authRepository.register(any(), any(), any(), any(), any(), any()) } returns
            ApiResult.Error(ApiError.BadRequest(message = "taken", errorKey = "user.existing_email"))

        val vm = viewModel()
        vm.fillValidForm()
        vm.register()
        advanceUntilIdle()

        coVerify(exactly = 0) { signupConsent.recordSignupTick(any(), any()) }
    }
}
