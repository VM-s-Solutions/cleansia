package cz.cleansia.customer.features.home

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The Home carousel loops over a bounded run of virtual pages. These pin the arithmetic that makes it a
 * loop: the pager opens on slide 0 with room either side, every page maps to a slide, and a slide-set
 * change keeps the slide on screen.
 */
class UpsellPagingTest {

    @Test
    fun `the pager opens on the first slide with room to swipe either way`() {
        (2..6).forEach { n ->
            val anchor = upsellAnchor(n)
            assertEquals("n=$n must open on slide 0", 0, upsellLogical(anchor, n))
            assertTrue("n=$n leaves no room to swipe back", anchor > n * 100)
            assertTrue("n=$n leaves no room to swipe forward", upsellVirtualCount(n) - anchor > n * 100)
        }
    }

    @Test
    fun `swiping forward from the last slide lands on the first, and back from the first on the last`() {
        val n = 3
        val first = upsellAnchor(n)
        assertEquals(n - 1, upsellLogical(first - 1, n))
        assertEquals(0, upsellLogical(first + n, n))
    }

    @Test
    fun `two slides still loop`() {
        val anchor = upsellAnchor(2)
        assertEquals(listOf(0, 1, 0, 1), (0..3).map { upsellLogical(anchor + it, 2) })
        assertEquals(1, upsellLogical(anchor - 1, 2))
    }

    @Test
    fun `nothing to loop gets one page and no virtual run`() {
        assertEquals(1, upsellVirtualCount(1))
        assertEquals(0, upsellAnchor(1))
        assertEquals(0, upsellLogical(0, 1))
        assertEquals(0, upsellVirtualCount(0))
    }

    private val before = listOf(UpsellKind.Plus, UpsellKind.Referral, UpsellKind.QuickSize)

    /** Slides arrive at the front, so the slide on screen moves index; the re-anchor follows it. */
    @Test
    fun `a slide arriving after first paint keeps the slide on screen`() {
        val onReferral = upsellAnchor(3) + 1 + 3 * 7
        val after = listOf(UpsellKind.Credit) + before
        val target = upsellReanchor(onReferral, before, after)
        assertEquals(UpsellKind.Referral, after[upsellLogical(target, after.size)])
    }

    @Test
    fun `a slide leaving while it was on screen keeps the position`() {
        val withNotifications = listOf(UpsellKind.Notifications) + before
        val onNotifications = upsellAnchor(withNotifications.size)
        val target = upsellReanchor(onNotifications, withNotifications, before)
        assertEquals(0, upsellLogical(target, before.size))
        assertTrue(target in 0 until upsellVirtualCount(before.size))
    }

    @Test
    fun `a set that shrank past the slide on screen lands on its last slide`() {
        val long = listOf(UpsellKind.Notifications, UpsellKind.Credit, UpsellKind.Plus, UpsellKind.Referral, UpsellKind.QuickSize)
        val onReferral = upsellAnchor(long.size) + 3
        val short = listOf(UpsellKind.Plus, UpsellKind.QuickSize)
        val target = upsellReanchor(onReferral, long, short)
        assertEquals(1, upsellLogical(target, short.size))
    }
}
