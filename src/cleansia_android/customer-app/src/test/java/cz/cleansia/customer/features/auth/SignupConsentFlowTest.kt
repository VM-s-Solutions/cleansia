package cz.cleansia.customer.features.auth

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.emptyPreferences
import cz.cleansia.core.auth.SessionManager
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.consent.PendingSignupConsent
import cz.cleansia.core.consent.SIGNUP_TICK_CONSENTS
import cz.cleansia.core.consent.SignupConsentRepository
import cz.cleansia.core.consent.SignupConsentStore
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.notifications.PushTokenRepository
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.api.client.GdprApi
import cz.cleansia.customer.api.model.ConsentType
import cz.cleansia.customer.api.model.GdprExportDto
import cz.cleansia.customer.api.model.GrantConsentCommand
import cz.cleansia.customer.api.model.UserConsentDto
import cz.cleansia.customer.api.model.WithdrawConsentCommand
import cz.cleansia.customer.core.auth.AuthApi
import cz.cleansia.customer.core.auth.AuthRepository
import cz.cleansia.customer.core.auth.AuthSuccess
import cz.cleansia.customer.core.auth.GoogleSignInController
import cz.cleansia.customer.core.auth.GoogleSignInResult
import cz.cleansia.customer.core.auth.JwtTokenResponseDto
import cz.cleansia.customer.core.consent.GdprConsentClient
import cz.cleansia.customer.core.settings.AppSettings
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.testing.MainDispatcherRule
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
 * Customer registration produces NO session: `api/Auth/Register` answers a bare boolean and
 * the user is routed to email verification, while `GrantConsent` needs an authenticated
 * caller. So the tick is parked at registration and delivered at the first token response
 * in which the server itself names the same account.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class SignupConsentFlowTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    private lateinit var authApi: AuthApi
    private lateinit var registerResult: ApiResult<Unit>
    private lateinit var gdprApi: RecordingGdprApi
    private lateinit var tokenStore: TokenStore
    private lateinit var settings: AppSettingsRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var googleSignInController: GoogleSignInController
    private lateinit var context: Context
    private lateinit var store: SignupConsentStore
    private lateinit var signupConsent: SignupConsentRepository

    private val email = "ada@example.com"

    @Before
    fun setUp() {
        authApi = mockk()
        registerResult = ApiResult.Success(Unit)
        gdprApi = RecordingGdprApi()
        tokenStore = mockk(relaxed = true)
        settings = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        googleSignInController = mockk(relaxed = true)
        context = mockk(relaxed = true)

        every { settings.settings } returns flowOf(AppSettings())
        coEvery { settings.emailLanguageTag() } returns "cs"
        every { context.packageName } returns "cz.cleansia.customer"
        every { context.resources } returns mockk(relaxed = true)

        store = SignupConsentStore(InMemoryPreferences())
        signupConsent = SignupConsentRepository(store, GdprConsentClient(gdprApi, json))
    }

    /**
     * The registration call itself is stubbed at the repository boundary rather than at
     * Retrofit: `safeApiCall` hops to `Dispatchers.IO`, which `advanceUntilIdle` cannot
     * await, so a real one would make the parked-tick assertions race the ViewModel.
     * The sign-in half below runs the real repository, awaited directly.
     */
    private fun TestScope.register(acceptedTerms: Boolean, address: String = email) {
        val repository = mockk<AuthRepository>(relaxed = true)
        coEvery { repository.register(any(), any(), any(), any(), any(), any()) } returns registerResult

        AuthViewModel(
            authRepository = repository,
            settings = settings,
            snackbar = snackbar,
            googleSignInController = googleSignInController,
            signupConsent = signupConsent,
            appContext = context,
        ).register(address, "Passw0rd!", "Ada", "Lovelace", acceptedTerms = acceptedTerms)

        advanceUntilIdle()
    }

    /**
     * [sessionEmail] is what the SERVER answers with. The sign-in form always carries the
     * registered address, so a helper that read the form instead of the token response
     * would look correct here and still deliver one account's tick into another's session.
     */
    private suspend fun signIn(sessionEmail: String? = email): ApiResult<*> {
        coEvery { authApi.login(any()) } returns Response.success(
            JwtTokenResponseDto(
                token = "header.payload.signature",
                isEmailConfirmed = true,
                userId = "u-1",
                email = sessionEmail,
                refreshToken = "r-1",
                refreshTokenExpiresAt = "2099-01-01T00:00:00Z",
            ),
        )
        return AuthRepository(
            api = authApi,
            authenticatedApi = javax.inject.Provider { authApi },
            tokenStore = tokenStore,
            sessionManager = mockk<SessionManager>(relaxed = true),
            sessionScopedCaches = emptySet(),
            pushTokenRepository = mockk<PushTokenRepository>(relaxed = true),
            signupConsent = javax.inject.Provider { signupConsent },
            json = json,
        ).login(email, "Passw0rd!", rememberMe = true)
    }

    /**
     * A social signup is the mirror image of the email one: the server answers it with a live
     * session, so the real repository flushes any parked tick from INSIDE that call, before the
     * ViewModel's next line runs. A tick parked afterwards therefore misses the only delivery the
     * flow performs. That ordering is unobservable from the grant list alone, so the stub reads
     * the park store at the instant the call goes out and hands it back.
     */
    private fun TestScope.googleFlow(launch: AuthViewModel.() -> Unit): PendingSignupConsent? {
        var parkedWhenTheCallWentOut: PendingSignupConsent? = null
        val repository = mockk<AuthRepository>(relaxed = true)
        coEvery { googleSignInController.signIn(any()) } returns GoogleSignInResult.Success(
            idToken = "google-id-token",
            googleId = "google-subject",
            email = email,
            firstName = "Ada",
            lastName = "Lovelace",
        )
        coEvery { repository.googleAuth(any(), any(), any(), any(), any(), any()) } coAnswers {
            parkedWhenTheCallWentOut = store.read()
            ApiResult.Success(
                AuthSuccess.Authenticated(
                    TokenStore.Tokens(
                        accessToken = "a",
                        accessTokenExpiresAt = 1L,
                        refreshToken = "r",
                        refreshTokenExpiresAt = 1L,
                    ),
                ),
            )
        }

        AuthViewModel(
            authRepository = repository,
            settings = settings,
            snackbar = snackbar,
            googleSignInController = googleSignInController,
            signupConsent = signupConsent,
            appContext = context,
        ).launch()

        advanceUntilIdle()
        return parkedWhenTheCallWentOut
    }

    private val granted: List<ConsentType?> get() = gdprApi.grantCommands.map { it.consentType }

    @Test
    fun aTickedBox_grantsTermsOfServiceAndPrivacyPolicyAndNothingElse() = runTest {
        register(acceptedTerms = true)

        signIn()

        assertEquals(listOf(ConsentType._0, ConsentType._1), granted)
    }

    @Test
    fun anUntickedBox_grantsNothing() = runTest {
        register(acceptedTerms = false)

        signIn()

        assertEquals(emptyList<ConsentType?>(), granted)
    }

    @Test
    fun aRejectedRegistration_grantsNothing() = runTest {
        registerResult = ApiResult.Error(
            ApiError.BadRequest(message = "A validation problem occurred.", errorKey = "user.existing_email"),
        )
        register(acceptedTerms = true)

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
        register(acceptedTerms = true)

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
        register(acceptedTerms = true)

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
        register(acceptedTerms = true)

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
        register(acceptedTerms = true, address = email)

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
        register(acceptedTerms = true)

        signIn()

        assertEquals(listOf(ConsentType._1), granted)
    }

    @Test
    fun aFailedConsentRead_grantsNothingAndKeepsTheTick() = runTest {
        gdprApi.consentsResponse = { Response.error(500, "".asProblem()) }
        register(acceptedTerms = true)

        signIn()
        assertEquals(emptyList<ConsentType?>(), granted)

        gdprApi.consentsResponse = { Response.success(emptyList()) }
        signIn()
        assertEquals(listOf(ConsentType._0, ConsentType._1), granted)
    }

    @Test
    fun aConsentEndpointThatThrows_leavesSignInWorking() = runTest {
        gdprApi.consentsResponse = { throw java.io.IOException("network down") }
        register(acceptedTerms = true)

        val result = signIn()

        assertTrue("expected sign-in to succeed but was $result", result is ApiResult.Success)
        assertEquals(emptyList<ConsentType?>(), granted)
    }

    @Test
    fun aTickedGoogleSignup_parksTheTickBeforeTheCallThatDeliversIt() = runTest {
        val parked = googleFlow { signUpWithGoogle(context, acceptedTerms = true) }

        assertEquals(SIGNUP_TICK_CONSENTS, parked?.types)
    }

    @Test
    fun aTickedGoogleSignup_grantsTermsOfServiceAndPrivacyPolicyAndNothingElse() = runTest {
        googleFlow { signUpWithGoogle(context, acceptedTerms = true) }

        signupConsent.deliverFor(email)

        assertEquals(listOf(ConsentType._0, ConsentType._1), granted)
    }

    @Test
    fun aGoogleSignIn_grantsNothing() = runTest {
        googleFlow { signInWithGoogle(context) }

        signupConsent.deliverFor(email)

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
