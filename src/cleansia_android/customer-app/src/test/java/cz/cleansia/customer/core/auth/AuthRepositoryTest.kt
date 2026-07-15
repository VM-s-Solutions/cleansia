package cz.cleansia.customer.core.auth

import cz.cleansia.core.auth.ForcedSignOutReason
import cz.cleansia.core.auth.RefreshResult
import cz.cleansia.core.auth.SessionManager
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult

import android.content.Context
import cz.cleansia.core.notifications.PushTokenRepository
import cz.cleansia.customer.R
import io.mockk.Runs
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.coVerifyOrder
import io.mockk.every
import io.mockk.just
import io.mockk.mockk
import io.mockk.verify
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * AuthRepository tests — the ApiResult<T> contract (ADR-0011). A success returns
 * the body in [ApiResult.Success]; a failure returns [ApiResult.Error] carrying
 * the typed [ApiError]. The repo no longer renders the snackbar message — the VM
 * does via [ApiErrorParser.parseToUserMessage]; these tests pin the same single
 * message the VM would surface. No Retrofit/DataStore — every collaborator is a fake.
 */
class AuthRepositoryTest {

    private lateinit var api: AuthApi
    private lateinit var tokenStore: TokenStore
    private lateinit var sessionManager: SessionManager
    private lateinit var pushTokenRepository: PushTokenRepository
    private lateinit var appContext: Context

    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    private val networkMessage = "Check your internet connection and try again."
    private val unknownMessage = "Something went wrong. Please try again."

    @Before
    fun setUp() {
        api = mockk()
        tokenStore = mockk(relaxed = true)
        sessionManager = mockk(relaxed = true)
        pushTokenRepository = mockk(relaxed = true)
        appContext = mockk(relaxed = true)

        every { appContext.getString(R.string.error_generic_network) } returns networkMessage
        every { appContext.getString(R.string.error_generic_unknown) } returns unknownMessage
        // Fallbacks the parser may hit for non-2xx without bodies
        every { appContext.getString(R.string.error_generic_unauthorized) } returns "Your session expired."
        every { appContext.getString(R.string.error_generic_server) } returns "Server unavailable."
        every { appContext.packageName } returns "cz.cleansia.customer"
        val resources = mockk<android.content.res.Resources>(relaxed = true)
        every { appContext.resources } returns resources
        every { resources.getIdentifier(any(), any(), any()) } returns 0
    }

    private fun newRepository(caches: Set<SessionScopedCache> = emptySet()): AuthRepository =
        AuthRepository(
            api = api,
            tokenStore = tokenStore,
            sessionManager = sessionManager,
            sessionScopedCaches = caches,
            pushTokenRepository = pushTokenRepository,
            json = json,
        )

    /** Mirrors how the consuming ViewModel turns an ApiError into the user-facing string. */
    private fun ApiResult<*>.surfacedMessage(): String =
        ApiErrorParser.parseToUserMessage(appContext, (this as ApiResult.Error).error)

    // ── login() ──

    @Test
    fun login_givenValidJwtResponse_persistsTokensAndReturnsAuthenticated() = kotlinx.coroutines.test.runTest {
        val refreshExp = "2099-01-01T00:00:00Z"
        // Use a real-shaped JWT-ish access token to exercise JwtDecoder; we
        // accept the fallback path (15-min default) when it can't decode.
        val accessToken = "header.payload.signature"
        val dto = JwtTokenResponseDto(
            token = accessToken,
            isEmailConfirmed = true,
            hasAdminAccess = true,
            userId = "u-1",
            email = "user@example.com",
            refreshToken = "r-1",
            refreshTokenExpiresAt = refreshExp,
        )
        coEvery { api.login(any()) } returns Response.success(dto)

        val result = newRepository().login("user@example.com", "pw", rememberMe = true)

        assertTrue("expected Success but was $result", result is ApiResult.Success)
        val data = (result as ApiResult.Success).data
        assertTrue(data is AuthSuccess.Authenticated)
        verify { tokenStore.save(match { it.refreshToken == "r-1" && it.accessToken == accessToken }) }
    }

    @Test
    fun login_givenHttp401_returnsErrorWithUnauthorizedMessage() = kotlinx.coroutines.test.runTest {
        val errorBody = """{}""".toResponseBody("application/json".toMediaType())
        coEvery { api.login(any()) } returns Response.error(401, errorBody)

        val result = newRepository().login("user@example.com", "wrong", rememberMe = false)

        assertTrue("expected Error but was $result", result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.Unauthorized)
        // The VM would surface the unauthorized fallback string.
        assertEquals("Your session expired.", result.surfacedMessage())
    }

    @Test
    fun login_givenHttp400WithProblemDetails_surfacesParsedDetail() = kotlinx.coroutines.test.runTest {
        val payload = """
            { "title": "Validation", "detail": "Invalid credentials." }
        """.trimIndent()
        val errorBody = payload.toResponseBody("application/problem+json".toMediaType())
        coEvery { api.login(any()) } returns Response.error(400, errorBody)

        val result = newRepository().login("user@example.com", "wrong", rememberMe = false)

        assertTrue(result is ApiResult.Error)
        assertEquals("Invalid credentials.", result.surfacedMessage())
    }

    @Test
    fun login_whenApiThrows_returnsErrorWithNetworkMessage() = kotlinx.coroutines.test.runTest {
        coEvery { api.login(any()) } throws java.io.IOException("boom")

        val result = newRepository().login("u", "p", rememberMe = false)

        assertTrue(result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.Network)
        assertEquals(networkMessage, result.surfacedMessage())
    }

    @Test
    fun login_givenEmailUnconfirmed_returnsEmailUnconfirmedWithEmail() = kotlinx.coroutines.test.runTest {
        val dto = JwtTokenResponseDto(
            token = "",
            isEmailConfirmed = false,
            email = "pending@example.com",
        )
        coEvery { api.login(any()) } returns Response.success(dto)

        val result = newRepository().login("pending@example.com", "pw", rememberMe = false)

        assertTrue(result is ApiResult.Success)
        val data = (result as ApiResult.Success).data
        assertTrue(data is AuthSuccess.EmailUnconfirmed)
        assertEquals("pending@example.com", (data as AuthSuccess.EmailUnconfirmed).email)
        verify(exactly = 0) { tokenStore.save(any()) }
    }

    @Test
    fun login_givenSuccessfulButMissingRefreshToken_returnsErrorAndDoesNotSave() = kotlinx.coroutines.test.runTest {
        // No refreshToken → toTokens() returns null → repo emits the "unknown" fallback.
        val dto = JwtTokenResponseDto(
            token = "h.p.s",
            isEmailConfirmed = true,
            refreshToken = null,
            refreshTokenExpiresAt = null,
        )
        coEvery { api.login(any()) } returns Response.success(dto)

        val result = newRepository().login("user@example.com", "pw", rememberMe = false)

        assertTrue(result is ApiResult.Error)
        assertEquals(unknownMessage, result.surfacedMessage())
        verify(exactly = 0) { tokenStore.save(any()) }
    }

    // ── register() (fire-and-forget → ApiResult<Unit>) ──

    @Test
    fun register_givenSuccess_returnsUnitSuccess() = kotlinx.coroutines.test.runTest {
        coEvery { api.register(any()) } returns Response.success(true)

        val result = newRepository().register(
            email = "user@example.com",
            password = "pw",
            firstName = "A",
            lastName = "B",
            language = "en",
        )

        assertTrue(result is ApiResult.Success)
        assertEquals(Unit, (result as ApiResult.Success).data)
    }

    @Test
    fun register_givenHttpError_returnsError() = kotlinx.coroutines.test.runTest {
        val errorBody = """{}""".toResponseBody("application/json".toMediaType())
        coEvery { api.register(any()) } returns Response.error(400, errorBody)

        val result = newRepository().register(
            email = "user@example.com",
            password = "pw",
            firstName = "A",
            lastName = "B",
            language = "en",
        )

        assertTrue(result is ApiResult.Error)
    }

    // ── logout() ──

    @Test
    fun logout_clearsAllSessionScopedCachesAndTokens() = kotlinx.coroutines.test.runTest {
        val cacheA = mockk<SessionScopedCache>(relaxed = true)
        val cacheB = mockk<SessionScopedCache>(relaxed = true)

        // No refresh token → repo skips api.logout() and goes straight to clears.
        every { tokenStore.current() } returns null

        val repo = newRepository(setOf(cacheA, cacheB))
        repo.logout()

        coVerify(exactly = 1) { cacheA.clear() }
        coVerify(exactly = 1) { cacheB.clear() }
        verify(exactly = 1) { tokenStore.clear() }
        verify { sessionManager.emitForcedSignOut(ForcedSignOutReason.UserInitiated) }
    }

    @Test
    fun logout_callsApiLogoutWhenRefreshTokenPresent() = kotlinx.coroutines.test.runTest {
        every { tokenStore.current() } returns TokenStore.Tokens(
            accessToken = "a",
            accessTokenExpiresAt = 1L,
            refreshToken = "r",
            refreshTokenExpiresAt = 1L,
        )
        coEvery { api.logout(any()) } returns Response.success(true)

        val repo = newRepository()
        repo.logout()

        coVerify { api.logout(LogoutRequest("r")) }
        verify { tokenStore.clear() }
    }

    @Test
    fun logout_continuesLocalCleanupWhenApiCallFails() = kotlinx.coroutines.test.runTest {
        every { tokenStore.current() } returns TokenStore.Tokens(
            accessToken = "a",
            accessTokenExpiresAt = 1L,
            refreshToken = "r",
            refreshTokenExpiresAt = 1L,
        )
        coEvery { api.logout(any()) } throws java.io.IOException("network down")

        val repo = newRepository()
        repo.logout()

        // API call failed but local state was still wiped, per the
        // "best-effort — if the backend call fails we still wipe local state"
        // contract documented in the source.
        verify(exactly = 1) { tokenStore.clear() }
        verify { sessionManager.emitForcedSignOut(ForcedSignOutReason.UserInitiated) }
    }

    @Test
    fun logout_clearsCachesBeforeTokens() = kotlinx.coroutines.test.runTest {
        val cache = mockk<SessionScopedCache>()
        coEvery { cache.clear() } just Runs
        every { tokenStore.current() } returns null
        every { tokenStore.clear() } just Runs

        newRepository(setOf(cache)).logout()

        // Source comment: "Wipe session-scoped caches before the token so any
        // future expansion of clear() still sees a valid auth context".
        coVerifyOrder {
            cache.clear()
            tokenStore.clear()
        }
    }

    // ── refresh() (RefreshClient impl) — terminal vs retryable classification ──

    @Test
    fun refresh_givenSuccessfulResponse_returnsSuccessWithTokens() = kotlinx.coroutines.test.runTest {
        val dto = JwtTokenResponseDto(
            token = "h.p.s",
            isEmailConfirmed = true,
            refreshToken = "new-r",
            refreshTokenExpiresAt = "2099-01-01T00:00:00Z",
        )
        coEvery { api.refreshToken(any()) } returns Response.success(dto)

        val result = newRepository().refresh("old-r")
        assertTrue("expected Success but was $result", result is RefreshResult.Success)
        assertEquals("new-r", (result as RefreshResult.Success).tokens.refreshToken)
    }

    @Test
    fun refresh_whenApiThrows_returnsUnavailable() = kotlinx.coroutines.test.runTest {
        coEvery { api.refreshToken(any()) } throws java.io.IOException("boom")
        assertEquals(RefreshResult.Unavailable, newRepository().refresh("r"))
    }

    @Test
    fun refresh_given401_returnsRejected() = kotlinx.coroutines.test.runTest {
        val errBody = "".toResponseBody("application/json".toMediaType())
        coEvery { api.refreshToken(any()) } returns Response.error(401, errBody)
        assertEquals(RefreshResult.Rejected, newRepository().refresh("r"))
    }

    @Test
    fun refresh_given429_returnsUnavailable() = kotlinx.coroutines.test.runTest {
        val errBody = "".toResponseBody("application/json".toMediaType())
        coEvery { api.refreshToken(any()) } returns Response.error(429, errBody)
        assertEquals(RefreshResult.Unavailable, newRepository().refresh("r"))
    }

    @Test
    fun refresh_given500_returnsUnavailable() = kotlinx.coroutines.test.runTest {
        val errBody = "".toResponseBody("application/json".toMediaType())
        coEvery { api.refreshToken(any()) } returns Response.error(500, errBody)
        assertEquals(RefreshResult.Unavailable, newRepository().refresh("r"))
    }

    @Test
    fun refresh_givenBusinessRejectionBody_returnsRejected() = kotlinx.coroutines.test.runTest {
        val errBody = """{"code":"Token","message":"auth.invalid_refresh_token"}"""
            .toResponseBody("application/json".toMediaType())
        coEvery { api.refreshToken(any()) } returns Response.error(400, errBody)
        assertEquals(RefreshResult.Rejected, newRepository().refresh("r"))
    }

    @Test
    fun refresh_givenSuccessWithUnusableBody_returnsUnavailable() = kotlinx.coroutines.test.runTest {
        // Missing refreshToken → toTokens() null → unknown/unparseable is retryable, not a sign-out.
        val dto = JwtTokenResponseDto(token = "h.p.s", isEmailConfirmed = true, refreshToken = null)
        coEvery { api.refreshToken(any()) } returns Response.success(dto)
        assertEquals(RefreshResult.Unavailable, newRepository().refresh("r"))
    }
}
