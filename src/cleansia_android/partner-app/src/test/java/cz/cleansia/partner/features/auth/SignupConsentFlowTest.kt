package cz.cleansia.partner.features.auth

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.emptyPreferences
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.consent.SignupConsentRepository
import cz.cleansia.core.consent.SignupConsentStore
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.notifications.PushTokenRepository
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.client.AuthApi
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.client.GdprApi
import cz.cleansia.partner.api.model.ConsentType
import cz.cleansia.partner.api.model.GdprExportDto
import cz.cleansia.partner.api.model.GrantConsentCommand
import cz.cleansia.partner.api.model.JwtTokenResponse
import cz.cleansia.partner.api.model.UserConsentDto
import cz.cleansia.partner.api.model.WithdrawConsentCommand
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.consent.GdprConsentClient
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.settings.AppSettings
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.auth.AuthRepositoryImpl
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import retrofit2.Response

/**
 * Signup consent, registration through delivery, over the app's REAL generated `GdprApi` —
 * every assertion is on the [GrantConsentCommand] instances that reach the client, because
 * the one mistake here that produces a false legal record is a command carrying the wrong
 * [ConsentType], and a "was it called" check passes on that just as happily.
 *
 * Partner registration produces NO session: `authRegisterEmployee` answers a bare boolean
 * and the cleaner is routed to the confirm-email screen, while `GrantConsent` needs an
 * authenticated caller. So the tick is parked at registration and delivered at the first
 * token response in which the server itself names the same account.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class SignupConsentFlowTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    private lateinit var authApi: AuthApi
    private lateinit var registerResult: ApiResult<Boolean>
    private lateinit var gdprApi: RecordingGdprApi
    private lateinit var appSettingsRepository: AppSettingsRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var context: Context
    private lateinit var signupConsent: SignupConsentRepository

    private val email = "ada@example.com"

    @Before
    fun setUp() {
        authApi = mockk()
        registerResult = ApiResult.Success(true)
        gdprApi = RecordingGdprApi()
        appSettingsRepository = mockk()
        snackbar = mockk(relaxed = true)
        errorTranslator = mockk(relaxed = true)
        context = mockk()

        every { context.getString(any()) } returns "validation message"
        every { appSettingsRepository.settings } returns flowOf(AppSettings())
        coEvery { appSettingsRepository.emailLanguageTag() } returns "cs"

        signupConsent = SignupConsentRepository(
            SignupConsentStore(InMemoryPreferences()),
            GdprConsentClient(gdprApi, json),
        )
    }

    /**
     * The registration call is stubbed at the repository boundary rather than at Retrofit:
     * `safeApiCall` hops to `Dispatchers.IO`, which `advanceUntilIdle` cannot await, so a
     * real one would make the parked-tick assertions race the ViewModel. The sign-in half
     * below runs the real repository, awaited directly.
     */
    private fun TestScope.register(acceptTerms: Boolean, address: String = email) {
        val repository = mockk<AuthRepository>(relaxed = true)
        coEvery { repository.register(any(), any(), any(), any(), any()) } returns registerResult

        val vm = RegisterViewModel(
            repository,
            errorTranslator,
            appSettingsRepository,
            snackbar,
            signupConsent,
            context,
        )
        vm.onFirstNameChange("Ada")
        vm.onLastNameChange("Lovelace")
        vm.onEmailChange(address)
        vm.onPasswordChange("Passw0rd")
        vm.onConfirmPasswordChange("Passw0rd")
        vm.onAcceptTermsChange(acceptTerms)
        vm.register()

        advanceUntilIdle()
    }

    /**
     * The unticked branch is unreachable through [RegisterViewModel.register] — the box is a
     * hard blocker there, pinned separately in `RegisterViewModelTest`. Parking is driven
     * directly so the second line of defence is still exercised.
     */
    private suspend fun park(accepted: Boolean, address: String = email) {
        signupConsent.recordSignupTick(address, accepted)
    }

    /**
     * [sessionEmail] is what the SERVER answers with. The sign-in form always carries the
     * registered address, so a helper that read the form instead of the token response
     * would look correct here and still deliver one account's tick into another's session.
     */
    private suspend fun signIn(sessionEmail: String? = email): ApiResult<*> {
        coEvery { authApi.authLogin(any()) } returns Response.success(
            JwtTokenResponse(
                token = "header.payload.signature",
                isEmailConfirmed = true,
                // Non-nullable on the C# record, so the wire always carries it and the mapper
                // refuses a payload that does not.
                hasAdminAccess = false,
                userId = "u-1",
                email = sessionEmail,
                refreshToken = "r-1",
                refreshTokenExpiresAt = "2099-01-01T00:00:00Z",
            ),
        )
        val employeeApi = mockk<EmployeeApi>()
        coEvery { employeeApi.employeeGetCurrentEmployee() } returns Response.error(
            500,
            "".asProblem(),
        )
        return AuthRepositoryImpl(
            authApi = authApi,
            authenticatedAuthApi = { authApi },
            employeeApi = employeeApi,
            tokenStore = mockk<TokenStore>(relaxed = true),
            userProfileStore = mockk<UserProfileStore>(relaxed = true),
            json = json,
            pushTokenRepository = mockk<PushTokenRepository>(relaxed = true),
            sessionScopedCaches = { emptySet<SessionScopedCache>() },
            signupConsent = { signupConsent },
        ).login(email, "Passw0rd", rememberMe = true)
    }

    private val granted: List<ConsentType?> get() = gdprApi.grantCommands.map { it.consentType }

    @Test
    fun aTickedBox_grantsTermsOfServiceAndPrivacyPolicyAndNothingElse() = runTest {
        register(acceptTerms = true)

        signIn()

        assertEquals(listOf(ConsentType._0, ConsentType._1), granted)
    }

    @Test
    fun anUntickedBox_grantsNothing() = runTest {
        park(accepted = false)

        signIn()

        assertEquals(emptyList<ConsentType?>(), granted)
    }

    @Test
    fun aRejectedRegistration_grantsNothing() = runTest {
        registerResult = ApiResult.Error(
            ApiError.BadRequest(message = "A validation problem occurred.", errorKey = "user.existing_email"),
        )
        register(acceptTerms = true)

        signIn()

        assertEquals(emptyList<ConsentType?>(), granted)
    }

    @Test
    fun aDuplicateRefusal_countsAsDeliveredAndIsNotRetried() = runTest {
        // The literal body GrantConsent's failure arm produces: CreateProblemDetails keys the
        // `errors` bag by Error.Code (the offending field) and values it with Error.Message.
        gdprApi.grantResponse = {
            Response.error(
                400,
                """
                {"title":"Bad Request","type":"ConsentType",
                 "detail":"gdpr.consent_already_granted","status":400,
                 "errors":{"ConsentType":"gdpr.consent_already_granted"}}
                """.trimIndent().asProblem(),
            )
        }
        register(acceptTerms = true)

        signIn()
        signIn()

        assertEquals(listOf(ConsentType._0, ConsentType._1), granted)
    }

    /**
     * The same refusal with the two `Error` slots swapped — the shape that shipped before the fix.
     * The dot code sat in the bag KEY and the VALUE every client resolves was prose, so the refusal
     * was unrecognisable and the tick was re-sent every session.
     */
    @Test
    fun aDuplicateRefusalWithSwappedSlots_isNotRecognised() = runTest {
        gdprApi.grantResponse = {
            Response.error(
                400,
                """
                {"title":"Bad Request","type":"gdpr.consent_already_granted",
                 "detail":"Consent already granted","status":400,
                 "errors":{"gdpr.consent_already_granted":"Consent already granted"}}
                """.trimIndent().asProblem(),
            )
        }
        register(acceptTerms = true)

        signIn()
        signIn()

        assertEquals(
            listOf(ConsentType._0, ConsentType._1, ConsentType._0, ConsentType._1),
            granted,
        )
    }

    @Test
    fun aRealFailure_keepsTheTickAndRetriesAtTheNextSession() = runTest {
        gdprApi.grantResponse = { Response.error(500, "".asProblem()) }
        register(acceptTerms = true)

        signIn()
        assertEquals(listOf(ConsentType._0, ConsentType._1), granted)

        gdprApi.grantResponse = { Response.success(Unit) }
        signIn()

        assertEquals(
            listOf(ConsentType._0, ConsentType._1, ConsentType._0, ConsentType._1),
            granted,
        )
    }

    @Test
    fun anotherAccountsSession_neverReceivesThisAccountsTick() = runTest {
        register(acceptTerms = true, address = email)

        signIn(sessionEmail = "someone.else@example.com")
        assertEquals(emptyList<ConsentType?>(), granted)

        signIn(sessionEmail = email)
        assertEquals(listOf(ConsentType._0, ConsentType._1), granted)
    }

    @Test
    fun aWithdrawnConsent_isNotResurrected() = runTest {
        gdprApi.consentsResponse = {
            Response.success(
                listOf(UserConsentDto(id = "c-1", consentType = ConsentType._0, isGranted = false)),
            )
        }
        register(acceptTerms = true)

        signIn()

        assertEquals(listOf(ConsentType._1), granted)
    }

    @Test
    fun aFailedConsentRead_grantsNothingAndKeepsTheTick() = runTest {
        gdprApi.consentsResponse = { Response.error(500, "".asProblem()) }
        register(acceptTerms = true)

        signIn()
        assertEquals(emptyList<ConsentType?>(), granted)

        gdprApi.consentsResponse = { Response.success(emptyList()) }
        signIn()
        assertEquals(listOf(ConsentType._0, ConsentType._1), granted)
    }

    @Test
    fun aConsentEndpointThatThrows_leavesSignInWorking() = runTest {
        gdprApi.consentsResponse = { throw java.io.IOException("network down") }
        register(acceptTerms = true)

        val result = signIn()

        assertTrue("expected sign-in to succeed but was $result", result is ApiResult.Success)
        assertEquals(emptyList<ConsentType?>(), granted)
    }

    private fun String.asProblem() = toResponseBody("application/problem+json".toMediaType())
}

private class RecordingGdprApi : GdprApi {

    val grantCommands = mutableListOf<GrantConsentCommand>()

    var consentsResponse: () -> Response<List<UserConsentDto>> = { Response.success(emptyList()) }
    var grantResponse: () -> Response<Unit> = { Response.success(Unit) }

    override suspend fun gdprGetMyConsents(): Response<List<UserConsentDto>> = consentsResponse()

    override suspend fun gdprGrantConsent(grantConsentCommand: GrantConsentCommand?): Response<Unit> {
        grantCommands += requireNotNull(grantConsentCommand)
        return grantResponse()
    }

    override suspend fun gdprDeleteMyAccount(): Response<Unit> = unsupported()

    override suspend fun gdprExportMyData(): Response<GdprExportDto> = unsupported()

    override suspend fun gdprWithdrawConsent(
        withdrawConsentCommand: WithdrawConsentCommand?,
    ): Response<Unit> = unsupported()

    private fun unsupported(): Nothing =
        throw UnsupportedOperationException("not part of the signup-consent flow")
}

/** Same contract as the on-disk preferences store, minus its own IO scope. */
private class InMemoryPreferences : DataStore<Preferences> {

    private val state = MutableStateFlow(emptyPreferences())

    override val data: Flow<Preferences> = state

    override suspend fun updateData(transform: suspend (Preferences) -> Preferences): Preferences =
        transform(state.value).also { state.value = it }
}
