package cz.cleansia.customer.core.auth

import android.util.Log
import cz.cleansia.customer.R
import cz.cleansia.customer.ui.snackbar.SnackbarController
import java.io.IOException
import okhttp3.Interceptor
import okhttp3.Response

/**
 * Emits a global snackbar for INFRASTRUCTURE failures only — things every
 * screen handles identically:
 *  - No connectivity / DNS / timeout (IOException)
 *  - Server errors (5xx)
 *
 * Business 400s are NOT handled here — screens show them inline next to the
 * field that caused them (login, signup, etc.). Each caller still gets the
 * original exception or Response; this interceptor never swallows.
 */
class NetworkErrorInterceptor(
    private val snackbarController: SnackbarController,
) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        val response: Response = try {
            chain.proceed(request)
        } catch (e: IOException) {
            // Coroutine cancellation cancels the underlying OkHttp call, which surfaces
            // as IOException("Canceled"). That's the consumer's choice (e.g. NavHost
            // popBackStack on forced sign-out), not an infrastructure failure — don't
            // snackbar for it. Also skip RefreshToken — the Authenticator surfaces that
            // path via its own ForcedSignOut flow.
            val isCancellation = chain.call().isCanceled()
            val isRefreshToken = request.url.encodedPath.contains("RefreshToken", ignoreCase = true)
            if (!isCancellation && !isRefreshToken) {
                snackbarController.showErrorKey(R.string.error_generic_network)
            }
            Log.w(TAG, "Network error on ${request.url}: ${e.message}")
            throw e
        }

        if (response.code in 500..599) {
            snackbarController.showErrorKey(R.string.error_generic_server)
            Log.w(TAG, "Server ${response.code} on ${request.url}")
        }

        return response
    }

    private companion object {
        const val TAG = "NetworkErrorInterceptor"
    }
}
