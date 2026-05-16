package cz.cleansia.core.network

import android.util.Log
import kotlinx.coroutines.CancellationException

/**
 * Wraps a suspending Retrofit call with the canonical error-handling contract.
 *
 * Why this exists — repos used to catch `Throwable` and show a snackbar from
 * the catch block, which produced two unrelated bugs:
 *
 *  1. **Coroutine-cancellation false positives.** When the user navigates away
 *     while a request is in flight, the coroutine cancels and OkHttp throws
 *     `IOException("Canceled")`. A bare `catch (t: Throwable)` swallowed the
 *     `CancellationException` too — same `Throwable` parent — and showed a
 *     "Check your internet connection" snackbar even though connectivity was
 *     fine. This was the cause of the toast on Home/Profile after a fast tab
 *     switch.
 *
 *  2. **Double snackbars.** `NetworkErrorInterceptor` already raises a single
 *     global toast for any infrastructure failure (IOException, 5xx). Repos
 *     that also surfaced their own snackbar from the catch produced a
 *     duplicate, layered on top.
 *
 * Contract:
 *  - `CancellationException` is re-thrown immediately (cooperative cancel must
 *    propagate, never be silently turned into a "failure" return).
 *  - Any other `Throwable` returns `null` after a single `Log.w` for crash
 *    triage. The user-facing toast is the interceptor's job — repos must NOT
 *    snackbar from catch.
 *  - Successful Retrofit responses are returned as-is; HTTP-level error
 *    handling (parsing `errorBody`, etc.) stays at the call site since each
 *    feature interprets 4xx differently.
 *
 * Usage:
 * ```kotlin
 * val resp = networkCall { api.getById(id) } ?: return null
 * if (!resp.isSuccessful) {
 *     val msg = ApiErrorParser.parseToUserMessage(ctx, resp.errorBody(), resp.code())
 *     snackbar.showError(msg)
 *     return null
 * }
 * return resp.body()
 * ```
 */
suspend inline fun <T> networkCall(
    tag: String = "NetworkCall",
    block: () -> T,
): T? {
    return try {
        block()
    } catch (ce: CancellationException) {
        // Cooperative cancellation — re-throw so structured concurrency works.
        // Catching this is what produced the spurious "internet connection"
        // toast on Home/Profile when the user nav'd away mid-request.
        throw ce
    } catch (t: Throwable) {
        Log.w(tag, "Network call failed: ${t.message}")
        null
    }
}
