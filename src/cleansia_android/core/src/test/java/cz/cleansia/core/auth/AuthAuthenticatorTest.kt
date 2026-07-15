package cz.cleansia.core.auth

import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import okhttp3.Protocol
import okhttp3.Request
import okhttp3.Response
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test
import java.io.IOException
import javax.inject.Provider

/**
 * AuthAuthenticator refresh-failure classification. TERMINAL ([RefreshResult.Rejected])
 * tears the session down: caches + tokens cleared, ForcedSignOut emitted. RETRYABLE
 * ([RefreshResult.Unavailable] or an unexpected throw) fails just the original request
 * and leaves the session fully intact — the next 401 attempts refresh again.
 */
class AuthAuthenticatorTest {

    private lateinit var tokenStore: TokenStore
    private lateinit var sessionManager: SessionManager
    private lateinit var cache: SessionScopedCache
    private lateinit var refreshClient: RefreshClient
    private lateinit var authenticator: AuthAuthenticator

    private val validTokens = TokenStore.Tokens(
        accessToken = "access-1",
        accessTokenExpiresAt = System.currentTimeMillis() + 15 * 60_000L,
        refreshToken = "refresh-1",
        refreshTokenExpiresAt = System.currentTimeMillis() + 24 * 3_600_000L,
    )

    private val newTokens = validTokens.copy(
        accessToken = "access-2",
        refreshToken = "refresh-2",
    )

    @Before
    fun setUp() {
        tokenStore = mockk(relaxed = true)
        sessionManager = mockk(relaxed = true)
        cache = mockk(relaxed = true)
        refreshClient = mockk()
        authenticator = AuthAuthenticator(
            tokenStore = tokenStore,
            sessionManager = sessionManager,
            sessionScopedCachesProvider = Provider { setOf(cache) },
            refreshClient = { refreshClient },
        )
        every { tokenStore.current() } returns validTokens
    }

    private fun http401(bearer: String = validTokens.accessToken): Response {
        val request = Request.Builder()
            .url("https://mobile.example.test/api/Order/GetOrders")
            .header("Authorization", "Bearer $bearer")
            .build()
        return Response.Builder()
            .request(request)
            .protocol(Protocol.HTTP_1_1)
            .code(401)
            .message("Unauthorized")
            .build()
    }

    // ── TERMINAL: the server rejected the refresh token ──

    @Test
    fun authenticate_whenRefreshRejected_clearsSessionAndEmitsForcedSignOut() {
        coEvery { refreshClient.refresh("refresh-1") } returns RefreshResult.Rejected

        val result = authenticator.authenticate(null, http401())

        assertNull(result)
        coVerify(exactly = 1) { cache.clear() }
        verify(exactly = 1) { tokenStore.clear() }
        verify(exactly = 1) { sessionManager.emitForcedSignOut(ForcedSignOutReason.SessionExpired) }
    }

    // ── RETRYABLE: transport / availability failures keep the session ──

    @Test
    fun authenticate_whenRefreshUnavailable_keepsTokensAndDoesNotEmit() {
        coEvery { refreshClient.refresh("refresh-1") } returns RefreshResult.Unavailable

        val result = authenticator.authenticate(null, http401())

        assertNull(result)
        coVerify(exactly = 0) { cache.clear() }
        verify(exactly = 0) { tokenStore.clear() }
        verify(exactly = 0) { tokenStore.save(any()) }
        verify(exactly = 0) { sessionManager.emitForcedSignOut(any()) }
    }

    @Test
    fun authenticate_whenRefreshThrows_treatsAsRetryable() {
        coEvery { refreshClient.refresh("refresh-1") } throws IOException("network down")

        val result = authenticator.authenticate(null, http401())

        assertNull(result)
        coVerify(exactly = 0) { cache.clear() }
        verify(exactly = 0) { tokenStore.clear() }
        verify(exactly = 0) { sessionManager.emitForcedSignOut(any()) }
    }

    @Test
    fun authenticate_afterRetryableFailure_nextTriggerAttemptsRefreshAgain() {
        coEvery { refreshClient.refresh("refresh-1") } returns
            RefreshResult.Unavailable andThen RefreshResult.Success(newTokens)

        assertNull(authenticator.authenticate(null, http401()))
        val retried = authenticator.authenticate(null, http401())

        assertNotNull(retried)
        assertEquals("Bearer access-2", retried!!.header("Authorization"))
        coVerify(exactly = 2) { refreshClient.refresh("refresh-1") }
        verify(exactly = 0) { sessionManager.emitForcedSignOut(any()) }
    }

    // ── Success path ──

    @Test
    fun authenticate_whenRefreshSucceeds_savesTokensAndRetriesWithNewToken() {
        coEvery { refreshClient.refresh("refresh-1") } returns RefreshResult.Success(newTokens)

        val result = authenticator.authenticate(null, http401())

        assertNotNull(result)
        assertEquals("Bearer access-2", result!!.header("Authorization"))
        verify(exactly = 1) { tokenStore.save(newTokens) }
        verify(exactly = 0) { sessionManager.emitForcedSignOut(any()) }
    }

    @Test
    fun authenticate_whenAnotherCallerAlreadyRefreshed_reusesStoredTokenWithoutNetwork() {
        every { tokenStore.current() } returns newTokens

        val result = authenticator.authenticate(null, http401(bearer = "access-1"))

        assertNotNull(result)
        assertEquals("Bearer access-2", result!!.header("Authorization"))
        coVerify(exactly = 0) { refreshClient.refresh(any()) }
    }

    // ── Guard paths ──

    @Test
    fun authenticate_whenRefreshTokenExpired_signsOutWithoutNetworkCall() {
        every { tokenStore.current() } returns validTokens.copy(
            refreshTokenExpiresAt = System.currentTimeMillis() - 1_000L,
        )

        val result = authenticator.authenticate(null, http401())

        assertNull(result)
        coVerify(exactly = 0) { refreshClient.refresh(any()) }
        verify(exactly = 1) { tokenStore.clear() }
        verify(exactly = 1) { sessionManager.emitForcedSignOut(ForcedSignOutReason.SessionExpired) }
    }

    @Test
    fun authenticate_whenAlreadyLoggedOut_returnsNullAndDoesNothing() {
        every { tokenStore.current() } returns null

        val result = authenticator.authenticate(null, http401())

        assertNull(result)
        coVerify(exactly = 0) { refreshClient.refresh(any()) }
        verify(exactly = 0) { sessionManager.emitForcedSignOut(any()) }
    }
}
