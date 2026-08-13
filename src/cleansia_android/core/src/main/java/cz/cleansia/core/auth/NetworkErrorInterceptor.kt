package cz.cleansia.core.auth

import android.util.Log
import cz.cleansia.core.snackbar.SnackbarController
import java.io.IOException
import java.io.InterruptedIOException
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
 *
 * Each app provides its own [networkErrorStringRes] + [serverErrorStringRes]
 * pointing at the locale-aware strings — `:core` can't reference each app's
 * `R` class so the IDs come in via constructor.
 */
class NetworkErrorInterceptor(
    private val snackbarController: SnackbarController,
    private val networkErrorStringRes: Int,
    private val serverErrorStringRes: Int,
) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        val response: Response = try {
            chain.proceed(request)
        } catch (e: IOException) {
            if (!shouldSurfaceToast(chain, request, e)) {
                // Path only — a full URL can carry query params (emails, codes) into release logs.
                Log.w(TAG, "Suppressed toast for ${request.url.encodedPath}: ${e.message}")
                throw e
            }
            snackbarController.showErrorKey(networkErrorStringRes)
            Log.w(TAG, "Network error on ${request.url.encodedPath}: ${e.message}")
            throw e
        }

        if (response.code in 500..599) {
            snackbarController.showErrorKey(serverErrorStringRes)
            Log.w(TAG, "Server ${response.code} on ${request.url.encodedPath}")
        }

        return response
    }

    /**
     * Is this a real infrastructure failure worth a toast, or a benign cancellation?
     *
     * **The cancel flag is set ASYNCHRONOUSLY**, so checking it alone misses cases where the exception is
     * thrown before it propagates — a fast tab switch or a pop on forced sign-out then shows the user an
     * infrastructure error. Message text, InterruptedIOException and a socket closed under a cancelled
     * call are the additional signals. -> /mobile-app/patterns#cancellation-noise
     */
    private fun shouldSurfaceToast(
        chain: Interceptor.Chain,
        request: okhttp3.Request,
        e: IOException,
    ): Boolean {
        if (chain.call().isCanceled()) return false
        if (request.url.encodedPath.contains("RefreshToken", ignoreCase = true)) return false
        if (e is InterruptedIOException) return false
        if (Thread.currentThread().isInterrupted) return false
        val msg = e.message?.lowercase().orEmpty()
        if ("canceled" in msg || "cancelled" in msg) return false
        if ("socket closed" in msg) return false
        if ("stream was reset" in msg) return false
        return true
    }

    private companion object {
        const val TAG = "NetworkErrorInterceptor"
    }
}
