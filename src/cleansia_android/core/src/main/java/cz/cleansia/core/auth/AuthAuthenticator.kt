package cz.cleansia.core.auth

import android.util.Log
import kotlinx.coroutines.runBlocking
import okhttp3.Authenticator
import okhttp3.Request
import okhttp3.Response
import okhttp3.Route

/**
 * Handles 401 responses by refreshing the access token once and retrying.
 *
 * OkHttp calls [authenticate] **only** when it receives a 401, and the returned
 * request is retried **exactly once**. That contract gives us natural loop
 * protection — no manual retry counter, no need to remember what we already
 * tried. If the retry also 401s, OkHttp surfaces the 401 to the caller.
 *
 * Single-flight: if 10 parallel requests all get 401, OkHttp still calls this
 * Authenticator once per failed request, but [synchronized] on the instance
 * serialises them, and the second caller sees the fresh token already written
 * to [TokenStore] and uses it without hitting the network.
 *
 * Refresh failure is classified via [RefreshResult]: only a server-side auth
 * rejection ([RefreshResult.Rejected]) tears the session down — caches and
 * tokens cleared, [ForcedSignOutReason.SessionExpired] emitted through
 * [SessionManager]. A transport/availability failure
 * ([RefreshResult.Unavailable]) returns null without touching the token store,
 * so only the original request fails and the next 401 retries the refresh.
 *
 * [refreshClient] is a lazy-initialised function rather than a direct
 * dependency to break the circular DI: the Authenticator is built into the
 * shared OkHttpClient, but needs Retrofit to call the refresh endpoint — and
 * Retrofit needs the OkHttpClient. Hilt wires `refreshClient` to point at a
 * separate, no-auth OkHttp instance used solely for refresh calls.
 *
 * INVARIANT: any repository that implements [SessionScopedCache] is
 * automatically cleared on forced sign-out — there's no source-of-truth list
 * to keep in sync with [AuthRepository]. Both clear-paths iterate the same
 * `Set<SessionScopedCache>` Hilt multibinding, so they cannot drift.
 */
class AuthAuthenticator(
    private val tokenStore: TokenStore,
    private val sessionManager: SessionManager,
    // Lazy-resolved Provider for the multibinding — breaks the DI cycle
    // introduced by the session caches transitively requiring the
    // AuthOkHttpClient that this Authenticator is installed into. Hilt builds
    // the repositories on first .get() call, by which time the OkHttpClient
    // already exists.
    private val sessionScopedCachesProvider: javax.inject.Provider<Set<@JvmSuppressWildcards SessionScopedCache>>,
    private val refreshClient: () -> RefreshClient,
) : Authenticator {

    override fun authenticate(route: Route?, response: Response): Request? = synchronized(this) {
        val currentTokens = tokenStore.current() ?: run {
            // Already logged out; nothing to do.
            return null
        }

        // If somebody else refreshed while we were queued, just use the new token.
        val requestAccess = response.request.header("Authorization")?.removePrefix("Bearer ")?.trim()
        if (requestAccess != null && requestAccess != currentTokens.accessToken) {
            return response.request.newBuilder()
                .header("Authorization", "Bearer ${currentTokens.accessToken}")
                .build()
        }

        if (currentTokens.isRefreshExpired()) {
            // No point trying — the refresh token itself is dead.
            Log.i(TAG, "Refresh token expired; signing out.")
            clearSessionCaches()
            tokenStore.clear()
            sessionManager.emitForcedSignOut(ForcedSignOutReason.SessionExpired)
            return null
        }

        // Blocking refresh call. OkHttp's Authenticator is a blocking API — we're
        // already on a background thread here (OkHttp dispatcher), so runBlocking
        // is acceptable and there's no way to make it suspend.
        val outcome = try {
            runBlocking { refreshClient().refresh(currentTokens.refreshToken) }
        } catch (t: Throwable) {
            Log.w(TAG, "Refresh call failed: ${t.message}")
            RefreshResult.Unavailable
        }

        return when (outcome) {
            is RefreshResult.Success -> {
                tokenStore.save(outcome.tokens)
                response.request.newBuilder()
                    .header("Authorization", "Bearer ${outcome.tokens.accessToken}")
                    .build()
            }

            RefreshResult.Rejected -> {
                Log.i(TAG, "Refresh rejected by the server; signing out.")
                clearSessionCaches()
                tokenStore.clear()
                sessionManager.emitForcedSignOut(ForcedSignOutReason.SessionExpired)
                null
            }

            RefreshResult.Unavailable -> {
                Log.i(TAG, "Refresh unavailable; keeping session, failing this request.")
                null
            }
        }
    }

    /**
     * Wipe every session-scoped cache. Iterates the [SessionScopedCache]
     * multibinding — same set [AuthRepository.logout] uses, so both clear
     * paths stay structurally in sync. OkHttp's Authenticator API is blocking,
     * so we runBlocking around the whole iteration (one suspending call per
     * cache, on this background thread).
     */
    private fun clearSessionCaches() {
        runBlocking {
            sessionScopedCachesProvider.get().forEach { it.clear() }
        }
    }

    private companion object {
        const val TAG = "AuthAuthenticator"
    }
}

/**
 * Minimal contract over the refresh endpoint — implementation lives in the
 * auth layer, wired via Hilt. Kept small so the Authenticator has no generated
 * DTO dependencies (those change when the API does; the Authenticator shouldn't).
 */
interface RefreshClient {
    /** Never throws for expected failures — transport errors map to [RefreshResult.Unavailable]. */
    suspend fun refresh(refreshToken: String): RefreshResult
}

/**
 * Outcome of a refresh attempt, classified so a transient failure cannot
 * destroy the session. With 30-minute access tokens the refresh path runs
 * dozens of times a day per device; a flaky network moment or a 429 from the
 * shared per-IP anonymous rate bucket must not sign the user out.
 *
 *  - [Rejected] — TERMINAL. The refresh endpoint answered with an auth
 *    rejection: HTTP 401/403, or a parseable business rejection
 *    (invalid/expired/revoked/reused refresh token). The tokens are dead;
 *    the session is torn down.
 *  - [Unavailable] — RETRYABLE. The endpoint gave no verdict:
 *    IOException/timeout/DNS/TLS failure, HTTP 5xx, HTTP 429, or any
 *    unknown/unparseable non-auth answer. Tokens are kept, only the original
 *    request fails, and the next 401 retries the refresh. Treating the unknown
 *    case as retryable is fail-open for the *session* only, not for access:
 *    every API call re-validates the access token server-side, so a genuinely
 *    revoked session still reaches nothing — it just gets signed out on the
 *    next refresh attempt the server actually answers.
 */
sealed interface RefreshResult {
    data class Success(val tokens: TokenStore.Tokens) : RefreshResult
    data object Rejected : RefreshResult
    data object Unavailable : RefreshResult

    companion object {
        // BusinessErrorMessage keys the backend writes into the refresh
        // rejection body; covers a rejection riding a non-401 status.
        private val rejectionKeys = listOf(
            "auth.invalid_refresh_token",
            "auth.refresh_token_reused",
        )

        /**
         * Classifies a non-2xx refresh response. Keep in lockstep with the
         * iOS SessionRefresher — the rule is cross-platform.
         */
        fun classifyHttpFailure(httpCode: Int, errorBody: String?): RefreshResult = when {
            httpCode == 401 || httpCode == 403 -> Rejected
            errorBody != null && rejectionKeys.any(errorBody::contains) -> Rejected
            else -> Unavailable
        }
    }
}
