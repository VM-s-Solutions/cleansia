package cz.cleansia.core.format

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class CleaningEstimateProgressTest {
    @Test
    fun `progress fills only the active cleaning estimate`() {
        val progress = cleaningEstimateProgress(1_000L, 10, 1_300L)!!
        assertEquals(0.5f, progress.fraction)
        assertEquals(5L, progress.remainingMinutes)
        assertEquals(0L, progress.overrunMinutes)
    }

    @Test
    fun `overrun rounds up and never advances the lifecycle`() {
        val progress = cleaningEstimateProgress(1_000L, 10, 1_661L)!!
        assertEquals(1f, progress.fraction)
        assertEquals(0L, progress.remainingMinutes)
        assertEquals(2L, progress.overrunMinutes)
    }

    @Test
    fun `the exact estimate is full without an overrun`() {
        val progress = cleaningEstimateProgress(1_000L, 10, 1_600L)!!
        assertEquals(1f, progress.fraction)
        assertEquals(0L, progress.overrunMinutes)
    }

    @Test
    fun `missing future or unestimated starts claim no progress`() {
        assertNull(cleaningEstimateProgress(null, 10, 1_000L))
        assertNull(cleaningEstimateProgress(1_001L, 10, 1_000L))
        assertNull(cleaningEstimateProgress(1_000L, 0, 1_001L))
    }
}
