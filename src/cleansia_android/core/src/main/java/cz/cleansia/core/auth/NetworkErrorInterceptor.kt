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
                Log.w(TAG, "Suppressed toast for ${request.url}: ${e.message}")
                throw e
            }
            snackbarController.showErrorKey(networkErrorStringRes)
            Log.w(TAG, "Network error on ${request.url}: ${e.message}")
            throw e
        }

        if (response.code in 500..599) {
            snackbarController.showErrorKey(serverErrorStringRes)
            Log.w(TAG, "Server ${response.code} on ${request.url}")
        }

        return response
    }

    /**
     * Decide whether THIS exception is a real infrastructure failure worth
     * showing the user a toast for, or a benign artefact we should swallow.
     *
     * The bug we're guarding against: when a screen unmounts mid-fetch (fast
     * tab switch on Home, NavHost popBackStack on forced sign-out, app
     * backgrounding) the coroutine cancels and OkHttp surfaces it as one of
     * several IOException subtypes. `chain.call().isCanceled()` catches some
     * of those but not all — the cancel flag is set asynchronously and
     * sometimes the exception is thrown before it propagates. We add three
     * additional "this is a cancel, not a real failure" signals:
     *
     *  1. Exception message contains "canceled" / "closed" — covers
     *     SocketException("Socket closed") + IOException("Canceled").
     *  2. InterruptedIOException — thread was interrupted; treat as cancel.
     *  3. The thread's interrupt flag is set — same.
     *
     * RefreshToken stays excluded — the Authenticator handles that path via
     * its own ForcedSignOut flow.
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
