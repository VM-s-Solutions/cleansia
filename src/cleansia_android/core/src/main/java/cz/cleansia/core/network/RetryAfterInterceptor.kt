package cz.cleansia.core.network

import java.io.IOException
import kotlin.random.Random
import okhttp3.Interceptor
import okhttp3.Response

/**
 * Honors the server's `429 Too Many Requests` + `Retry-After` contract
 * (partitioned rate limiter, ADR-0003 D6): waits the advertised delay plus a
 * random 0–15s jitter — so rejected clients desync instead of re-spiking at
 * the window rollover — then retries the request exactly **once**. A second
 * `429` (or any other status) is returned to the caller unchanged.
 *
 * Belongs **outermost** on the auth OkHttp chain so the retry re-enters
 * [cz.cleansia.core.auth.AuthInterceptor] and picks up a fresh Bearer token.
 * The refresh/login NoAuth client deliberately does not carry it.
 *
 * The wait is cancellation-safe: `call.cancel()` (what a cancelled coroutine
 * triggers) is observed between sleep slices and aborts with the same
 * `IOException("Canceled")` OkHttp itself uses, which the toast suppression in
 * [cz.cleansia.core.auth.NetworkErrorInterceptor] already recognizes.
 */
class RetryAfterInterceptor(
    private val jitterMillis: () -> Long = { Random.nextLong(JITTER_RANGE_MILLIS) },
) : Interceptor {

    override fun intercept(chain: Interceptor.Chain): Response {
        val response = chain.proceed(chain.request())
        if (response.code != HTTP_TOO_MANY_REQUESTS) return response

        val delayMillis = backoffMillis(response.header(RETRY_AFTER_HEADER))
        response.close()
        awaitBackoff(chain, delayMillis)
        return chain.proceed(chain.request())
    }

    internal fun backoffMillis(retryAfterHeader: String?): Long {
        val baseMillis = retryAfterHeader?.trim()
            ?.toLongOrNull()
            ?.takeIf { it >= 0 }
            ?.times(1_000)
            ?: DEFAULT_BACKOFF_MILLIS
        return baseMillis + jitterMillis()
    }

    private fun awaitBackoff(chain: Interceptor.Chain, totalMillis: Long) {
        var remainingMillis = totalMillis
        while (remainingMillis > 0) {
            throwIfCanceled(chain)
            val sliceMillis = minOf(WAIT_SLICE_MILLIS, remainingMillis)
            try {
                Thread.sleep(sliceMillis)
            } catch (e: InterruptedException) {
                Thread.currentThread().interrupt()
                throw IOException("Canceled", e)
            }
            remainingMillis -= sliceMillis
        }
        throwIfCanceled(chain)
    }

    private fun throwIfCanceled(chain: Interceptor.Chain) {
        if (chain.call().isCanceled()) throw IOException("Canceled")
    }

    private companion object {
        const val HTTP_TOO_MANY_REQUESTS = 429
        const val RETRY_AFTER_HEADER = "Retry-After"
        const val DEFAULT_BACKOFF_MILLIS = 60_000L
        const val JITTER_RANGE_MILLIS = 15_000L
        const val WAIT_SLICE_MILLIS = 250L
    }
}
