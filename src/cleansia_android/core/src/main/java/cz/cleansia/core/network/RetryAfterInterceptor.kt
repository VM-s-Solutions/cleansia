package cz.cleansia.core.network

import java.io.IOException
import kotlin.random.Random
import okhttp3.Interceptor
import okhttp3.Response

/**
 * Honours the server's 429 + Retry-After contract: waits the advertised delay plus random jitter — so
 * rejected clients desync instead of re-spiking at the window rollover — then retries exactly ONCE.
 *
 * **Belongs OUTERMOST on the chain** so the wait happens before anything else re-runs.
 * -> /flows/cross-cutting#rate-limiting
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
