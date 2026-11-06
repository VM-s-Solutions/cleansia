package cz.cleansia.core.ui.components

import androidx.compose.animation.core.exponentialDecay
import androidx.compose.animation.core.spring
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.gestures.AnchoredDraggableState
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.dp
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

@OptIn(ExperimentalFoundationApi::class)
class SnapSheetStateTest {

    private val density = Density(1f)

    private fun state(initial: SnapAnchor = SnapAnchor.Peek, stops: Set<SnapAnchor> = SnapAnchor.entries.toSet()) = SnapSheetState(
        draggable = AnchoredDraggableState(
            initialValue = initial,
            positionalThreshold = { it * 0.4f },
            velocityThreshold = { 500f },
            snapAnimationSpec = spring(),
            decayAnimationSpec = exponentialDecay(),
        ),
        minSheetHeight = 240.dp,
        density = density,
        stops = stops,
    )

    @Test
    fun `by default the sheet parks at all three anchors, where it always did`() {
        val sheet = state()
        sheet.onLayout(containerHeightPx = 800f, minSheetHeightPx = 240f, minSheetTopPx = 0f)

        val anchors = sheet.draggable.anchors
        SnapAnchor.entries.forEach { assertEquals(snapSheetTopPx(it, 800f, 240f), anchors.positionOf(it), 0.001f) }
    }

    @Test
    fun `only the stops it is given are anchors`() {
        val sheet = state(initial = SnapAnchor.Expanded, stops = setOf(SnapAnchor.MapFocus, SnapAnchor.Expanded))
        sheet.onLayout(containerHeightPx = 800f, minSheetHeightPx = 0f, minSheetTopPx = 150f)

        val anchors = sheet.draggable.anchors
        assertTrue(anchors.positionOf(SnapAnchor.Peek).isNaN())
        assertFalse(anchors.positionOf(SnapAnchor.MapFocus).isNaN())
        assertEquals(150f, anchors.positionOf(SnapAnchor.Expanded), 0.001f)
        assertEquals(560f, anchors.positionOf(SnapAnchor.MapFocus), 0.001f)
    }

    @Test
    fun `a new floor moves the anchors and keeps the sheet where it rests`() {
        val sheet = state(initial = SnapAnchor.Expanded, stops = setOf(SnapAnchor.MapFocus, SnapAnchor.Expanded))
        sheet.onLayout(containerHeightPx = 800f, minSheetHeightPx = 0f, minSheetTopPx = 150f)
        sheet.onLayout(containerHeightPx = 800f, minSheetHeightPx = 300f, minSheetTopPx = 150f)

        assertEquals(500f, sheet.draggable.anchors.positionOf(SnapAnchor.MapFocus), 0.001f)
        assertEquals(SnapAnchor.Expanded, sheet.currentAnchor)
        assertEquals(150f, sheet.sheetTopPx, 0.001f)
    }

    @Test
    fun `a map focus height moves only the collapsed anchor`() {
        val sheet = state(initial = SnapAnchor.Expanded, stops = setOf(SnapAnchor.MapFocus, SnapAnchor.Expanded))
        sheet.onLayout(containerHeightPx = 800f, minSheetHeightPx = 182f, minSheetTopPx = 150f, mapFocusHeightPx = 182f)

        assertEquals(618f, sheet.draggable.anchors.positionOf(SnapAnchor.MapFocus), 0.001f)
        assertEquals(150f, sheet.draggable.anchors.positionOf(SnapAnchor.Expanded), 0.001f)
    }
}
