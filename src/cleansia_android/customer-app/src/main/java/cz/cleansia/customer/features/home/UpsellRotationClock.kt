package cz.cleansia.customer.features.home

internal data class UpsellRotationTick(val progress: Float, val advance: Boolean)

internal class UpsellRotationClock {
    private var startedAt: Long? = null
    private var page: Int? = null
    private var advanced = false

    fun restart() {
        startedAt = null
        advanced = false
    }

    fun tick(nowMillis: Long, settledPage: Int, eligible: Boolean, scrolling: Boolean): UpsellRotationTick {
        if (!eligible || scrolling) {
            restart()
            page = settledPage
            return UpsellRotationTick(0f, false)
        }
        if (page != settledPage) restart()
        page = settledPage
        val start = startedAt ?: nowMillis.also { startedAt = it }
        val progress = ((nowMillis - start).coerceAtLeast(0).toFloat() / DWELL_MILLIS).coerceIn(0f, 1f)
        val advance = progress >= 1f && !advanced
        if (advance) advanced = true
        return UpsellRotationTick(progress, advance)
    }

    private companion object {
        const val DWELL_MILLIS = 6_000L
    }
}
