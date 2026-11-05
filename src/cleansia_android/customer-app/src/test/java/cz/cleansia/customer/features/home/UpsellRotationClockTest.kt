package cz.cleansia.customer.features.home

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class UpsellRotationClockTest {
    @Test
    fun `fill and advance share one six second dwell`() {
        val clock = UpsellRotationClock()
        assertEquals(0f, clock.tick(1_000, 4, eligible = true, scrolling = false).progress, 0f)
        assertEquals(0.5f, clock.tick(4_000, 4, eligible = true, scrolling = false).progress, 0f)
        assertFalse(clock.tick(6_999, 4, eligible = true, scrolling = false).advance)
        assertTrue(clock.tick(7_000, 4, eligible = true, scrolling = false).advance)
    }

    @Test
    fun `a late manual swipe pauses through settling then starts a full dwell`() {
        val clock = UpsellRotationClock()
        clock.tick(0, 4, eligible = true, scrolling = false)
        clock.tick(5_900, 4, eligible = true, scrolling = false)
        clock.restart()
        assertEquals(0f, clock.tick(5_950, 4, eligible = true, scrolling = true).progress, 0f)
        assertFalse(clock.tick(6_500, 5, eligible = true, scrolling = true).advance)
        assertEquals(0f, clock.tick(7_000, 5, eligible = true, scrolling = false).progress, 0f)
        assertEquals(0.5f, clock.tick(10_000, 5, eligible = true, scrolling = false).progress, 0f)
        assertFalse(clock.tick(12_999, 5, eligible = true, scrolling = false).advance)
        assertTrue(clock.tick(13_000, 5, eligible = true, scrolling = false).advance)
    }

    @Test
    fun `a cancelled drag on the same page also restarts the dwell`() {
        val clock = UpsellRotationClock()
        clock.tick(0, 4, eligible = true, scrolling = false)
        clock.restart()
        clock.tick(5_000, 4, eligible = true, scrolling = true)
        clock.tick(6_000, 4, eligible = true, scrolling = false)
        assertFalse(clock.tick(11_999, 4, eligible = true, scrolling = false).advance)
        assertTrue(clock.tick(12_000, 4, eligible = true, scrolling = false).advance)
    }

    @Test
    fun `page changes reset stale time even when no gesture frame was observed`() {
        val clock = UpsellRotationClock()
        clock.tick(0, 4, eligible = true, scrolling = false)
        assertEquals(0f, clock.tick(5_900, 5, eligible = true, scrolling = false).progress, 0f)
        assertFalse(clock.tick(11_899, 5, eligible = true, scrolling = false).advance)
        assertTrue(clock.tick(11_900, 5, eligible = true, scrolling = false).advance)
    }

    @Test
    fun `offscreen background motion and accessibility pauses discard elapsed time`() {
        val clock = UpsellRotationClock()
        clock.tick(0, 4, eligible = true, scrolling = false)
        assertFalse(clock.tick(5_000, 4, eligible = false, scrolling = false).advance)
        assertEquals(0f, clock.tick(80_000, 4, eligible = true, scrolling = false).progress, 0f)
        assertFalse(clock.tick(85_999, 4, eligible = true, scrolling = false).advance)
        assertTrue(clock.tick(86_000, 4, eligible = true, scrolling = false).advance)
    }

    @Test
    fun `quick size control interaction restarts the same page`() {
        val clock = UpsellRotationClock()
        clock.tick(0, 4, eligible = true, scrolling = false)
        clock.restart()
        assertEquals(0f, clock.tick(5_000, 4, eligible = true, scrolling = false).progress, 0f)
        assertFalse(clock.tick(10_999, 4, eligible = true, scrolling = false).advance)
        assertTrue(clock.tick(11_000, 4, eligible = true, scrolling = false).advance)
    }

    @Test
    fun `advance is emitted once until the next dwell`() {
        val clock = UpsellRotationClock()
        clock.tick(0, 4, eligible = true, scrolling = false)
        assertTrue(clock.tick(6_000, 4, eligible = true, scrolling = false).advance)
        assertFalse(clock.tick(6_001, 4, eligible = true, scrolling = false).advance)
        clock.tick(6_100, 4, eligible = true, scrolling = true)
        clock.tick(6_600, 5, eligible = true, scrolling = false)
        assertTrue(clock.tick(12_600, 5, eligible = true, scrolling = false).advance)
    }
}
