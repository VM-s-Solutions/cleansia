package cz.cleansia.core.freshness

import java.util.concurrent.atomic.AtomicLong

/**
 * Per-cache freshness watermark. Repositories hold one instance per logical
 * cache (e.g. "my-invoices", "available-orders") and call [markFresh] after a
 * successful fetch. Consumer ViewModels check [isStale] before deciding to
 * trigger a background refresh on screen entry — fresh cache => skip the
 * network round-trip, show what we already have.
 *
 * User-initiated pulls bypass this check entirely (the user's intent is the
 * source of truth, not the cache age).
 *
 * Orthogonal to [cz.cleansia.core.auth.SessionScopedCache]: a repo may hold
 * both, and should call [reset] from its existing `SessionScopedCache.clear()`
 * so the watermark doesn't survive a logout / session swap.
 *
 * Thread-safe: backed by an [AtomicLong] so concurrent fetch completions don't
 * race the staleness check.
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
