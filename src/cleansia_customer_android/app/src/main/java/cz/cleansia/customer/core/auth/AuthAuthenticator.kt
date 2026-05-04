package cz.cleansia.customer.core.auth

import android.util.Log
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.disputes.DisputeRepository
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.referral.ReferralRepository
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
 * Refresh failure: emits a [ForcedSignOutReason.SessionExpired] event through
 * [SessionManager] and returns null, which tells OkHttp to stop trying.
 *
 * [refreshClient] is a lazy-initialised function rather than a direct
 * dependency to break the circular DI: the Authenticator is built into the
 * shared OkHttpClient, but needs Retrofit to call the refresh endpoint — and
 * Retrofit needs the OkHttpClient. Hilt wires `refreshClient` to point at a
 * separate, no-auth OkHttp instance used solely for refresh calls.
 */
class AuthAuthenticator(
    private val tokenStore: TokenStore,
    private val sessionManager: SessionManager,
    // Lazy-resolved Providers — break the DI cycle introduced by the session
    // caches transitively requiring the AuthOkHttpClient that this Authenticator
    // is installed into. Hilt builds the repositories on first .get() call, by
    // which time the OkHttpClient already exists.
    private val addressRepositoryProvider: javax.inject.Provider<AddressRepository>,
    private val orderRepositoryProvider: javax.inject.Provider<OrderRepository>,
    private val disputeRepositoryProvider: javax.inject.Provider<DisputeRepository>,
    private val loyaltyRepositoryProvider: javax.inject.Provider<LoyaltyRepository>,
    private val referralRepositoryProvider: javax.inject.Provider<ReferralRepository>,
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
            runBlocking { addressRepositoryProvider.get().clear() }
            runBlocking { orderRepositoryProvider.get().clear() }
            runBlocking { disputeRepositoryProvider.get().clear() }
            runBlocking { loyaltyRepositoryProvider.get().clear() }
            runBlocking { referralRepositoryProvider.get().clear() }
            tokenStore.clear()
            sessionManager.emitForcedSignOut(ForcedSignOutReason.SessionExpired)
            return null
        }

        // Blocking refresh call. OkHttp's Authenticator is a blocking API — we're
        // already on a background thread here (OkHttp dispatcher), so runBlocking
        // is acceptable and there's no way to make it suspend.
        val refreshed = try {
            runBlocking { refreshClient().refresh(currentTokens.refreshToken) }
        } catch (t: Throwable) {
            Log.w(TAG, "Refresh call failed: ${t.message}")
            null
        }

        if (refreshed == null) {
            // Server said the token is invalid, expired, or compromised. Either way, sign out.
            runBlocking { addressRepositoryProvider.get().clear() }
            runBlocking { orderRepositoryProvider.get().clear() }
            runBlocking { disputeRepositoryProvider.get().clear() }
            runBlocking { loyaltyRepositoryProvider.get().clear() }
            runBlocking { referralRepositoryProvider.get().clear() }
            tokenStore.clear()
            sessionManager.emitForcedSignOut(ForcedSignOutReason.SessionExpired)
            return null
        }

        tokenStore.save(refreshed)
        return response.request.newBuilder()
            .header("Authorization", "Bearer ${refreshed.accessToken}")
            .build()
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
    /** Returns the refreshed token bundle, or null if the server rejected the refresh. */
    suspend fun refresh(refreshToken: String): TokenStore.Tokens?
}
