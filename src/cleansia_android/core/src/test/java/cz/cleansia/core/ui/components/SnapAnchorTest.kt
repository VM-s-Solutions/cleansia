package cz.cleansia.core.ui.components

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Kotlin half of the iOS `SnapSheetSnapResolutionTests` contract: the three
 * anchors, their ordering, and the top-edge math both platforms park on.
 * Gesture physics belong to `AnchoredDraggableState`; the numbers do not, and
 * they are the ones that have to match iOS.
 */
class SnapAnchorTest {

    private val height = 800f

    @Test
    fun `three anchors are ordered by coverage`() {
        assertTrue(SnapAnchor.MapFocus.coveredFraction < SnapAnchor.Peek.coveredFraction)
        assertTrue(SnapAnchor.Peek.coveredFraction < SnapAnchor.Expanded.coveredFraction)
    }

    @Test
    fun `anchor fractions match the iOS SnapAnchor values`() {
        assertEquals(0.30f, SnapAnchor.MapFocus.coveredFraction, 0.0001f)
        assertEquals(0.75f, SnapAnchor.Peek.coveredFraction, 0.0001f)
        assertEquals(0.95f, SnapAnchor.Expanded.coveredFraction, 0.0001f)
    }

    @Test
    fun `map focus leaves the majority of the container to the backdrop`() {
        assertTrue(SnapAnchor.MapFocus.coveredFraction < 0.5f)
    }

    @Test
    fun `sheet top is the complement of the covered fraction`() {
        assertEquals(height * 0.70f, snapSheetTopPx(SnapAnchor.MapFocus, height), 0.001f)
        assertEquals(height * 0.25f, snapSheetTopPx(SnapAnchor.Peek, height), 0.001f)
        assertEquals(height * 0.05f, snapSheetTopPx(SnapAnchor.Expanded, height), 0.001f)
    }

    @Test
    fun `tops are strictly ordered so the anchors never collapse into each other`() {
        val tops = SnapAnchor.entries.map { snapSheetTopPx(it, height) }
        assertEquals(tops.sortedDescending(), tops)
        assertEquals(tops.distinct().size, tops.size)
    }

    @Test
    fun `min height floor stops map focus short on a container too short for the footer`() {
        val short = 600f
        val floor = 240f
        assertEquals(short - floor, snapSheetTopPx(SnapAnchor.MapFocus, short, floor), 0.001f)
    }

    @Test
    fun `min height floor does not move the deeper anchors`() {
        val floor = 240f
        assertEquals(height * 0.25f, snapSheetTopPx(SnapAnchor.Peek, height, floor), 0.001f)
        assertEquals(height * 0.05f, snapSheetTopPx(SnapAnchor.Expanded, height, floor), 0.001f)
    }

    @Test
    fun `a floor taller than the container never pushes the sheet above the top edge`() {
        assertEquals(0f, snapSheetTopPx(SnapAnchor.MapFocus, 200f, 900f), 0.001f)
    }

    @Test
    fun `an unmeasured container resolves to zero rather than a NaN offset`() {
        SnapAnchor.entries.forEach {
            assertEquals(0f, snapSheetTopPx(it, 0f), 0.001f)
            assertEquals(0f, snapSheetTopPx(it, -1f), 0.001f)
        }
    }

    @Test
    fun `no anchor ever hides the sheet`() {
        SnapAnchor.entries.forEach {
            assertTrue(
                "$it must leave the sheet on screen",
                snapSheetTopPx(it, height) < height,
            )
            assertTrue(it.coveredFraction > 0f)
        }
    }

    @Test
    fun `a minimum top holds the deepest anchor under it and leaves the lower ones alone`() {
        assertEquals(150f, snapSheetTopPx(SnapAnchor.Expanded, height, minSheetTopPx = 150f), 0.001f)
        assertEquals(height * 0.25f, snapSheetTopPx(SnapAnchor.Peek, height, minSheetTopPx = 150f), 0.001f)
        assertEquals(height * 0.70f, snapSheetTopPx(SnapAnchor.MapFocus, height, minSheetTopPx = 150f), 0.001f)
    }

    @Test
    fun `a height floor never lifts the sheet above its minimum top`() {
        assertEquals(150f, snapSheetTopPx(SnapAnchor.MapFocus, height, minSheetHeightPx = 760f, minSheetTopPx = 150f), 0.001f)
    }

    @Test
    fun `a given map focus height is where the collapsed sheet rests and the other anchors keep theirs`() {
        assertEquals(height - 182f, snapSheetTopPx(SnapAnchor.MapFocus, height, mapFocusHeightPx = 182f), 0.001f)
        assertEquals(height - 260f, snapSheetTopPx(SnapAnchor.MapFocus, height, mapFocusHeightPx = 260f), 0.001f)
        assertEquals(150f, snapSheetTopPx(SnapAnchor.Expanded, height, minSheetTopPx = 150f, mapFocusHeightPx = 182f), 0.001f)
        assertEquals(snapSheetTopPx(SnapAnchor.Peek, height), snapSheetTopPx(SnapAnchor.Peek, height, mapFocusHeightPx = 182f), 0.001f)
        assertEquals("the floor still holds", height - 240f, snapSheetTopPx(SnapAnchor.MapFocus, height, minSheetHeightPx = 240f, mapFocusHeightPx = 182f), 0.001f)
        assertEquals(0f, snapSheetTopPx(SnapAnchor.MapFocus, height, mapFocusHeightPx = 900f), 0.001f)
    }

    @Test
    fun `a zero minimum top is the top edge math as before`() {
        SnapAnchor.entries.forEach {
            assertEquals(snapSheetTopPx(it, height, 240f), snapSheetTopPx(it, height, 240f, minSheetTopPx = 0f), 0.0001f)
        }
    }
}
