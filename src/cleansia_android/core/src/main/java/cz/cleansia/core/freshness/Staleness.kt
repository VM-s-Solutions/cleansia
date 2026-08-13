package cz.cleansia.core.freshness

import java.util.concurrent.atomic.AtomicLong

/**
 * Per-cache freshness watermark. Repositories hold one per logical cache and mark it after a successful
 * fetch; ViewModels check it before triggering a background refresh on screen entry.
 *
 * **User-initiated pulls bypass it entirely** — the user's intent is the source of truth, not cache age.
 * Orthogonal to the session-scoped wipe: a repo may hold both and should reset this from its existing
 * wipe hook. -> /mobile-app/patterns#session-wipe
 */
class Staleness {
    private val lastFetchedAtMillis = AtomicLong(NEVER)

    /** Epoch millis of the last successful fetch, or `null` if never fetched. */
    val lastFetchedAt: Long?
        get() = lastFetchedAtMillis.get().takeIf { it != NEVER }

    /** Stamp the watermark with the current time. Call after a successful fetch. */
    fun markFresh() {
        lastFetchedAtMillis.set(System.currentTimeMillis())
    }

    /**
     * `true` if the cache has never been fetched, or if it was last fetched
     * more than [maxAgeMs] ago. Default is [DEFAULT_MAX_AGE_MS].
     */
    fun isStale(maxAgeMs: Long = DEFAULT_MAX_AGE_MS): Boolean {
        val stamp = lastFetchedAtMillis.get()
        if (stamp == NEVER) return true
        return (System.currentTimeMillis() - stamp) > maxAgeMs
    }

    /** Forget the watermark. Call from `SessionScopedCache.clear()` on logout. */
    fun reset() {
        lastFetchedAtMillis.set(NEVER)
    }

    companion object {
        const val DEFAULT_MAX_AGE_MS: Long = 30_000L
        private const val NEVER = 0L
    }
}
