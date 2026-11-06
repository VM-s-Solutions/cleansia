package cz.cleansia.core.ui.components

import androidx.compose.animation.core.spring
import androidx.compose.animation.rememberSplineBasedDecay
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.AnchoredDraggableState
import androidx.compose.foundation.gestures.DraggableAnchors
import androidx.compose.foundation.gestures.Orientation
import androidx.compose.foundation.gestures.anchoredDraggable
import androidx.compose.foundation.gestures.animateTo
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.Stable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.input.nestedscroll.NestedScrollConnection
import androidx.compose.ui.input.nestedscroll.NestedScrollSource
import androidx.compose.ui.input.nestedscroll.nestedScroll
import androidx.compose.ui.layout.layout
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.Velocity
import androidx.compose.ui.unit.dp
import kotlin.math.roundToInt

object SnapSheetDefaults {
    /**
     * Shortest the sheet is allowed to get. A sticky action footer stops fitting
     * below roughly this height, and a footer the user cannot reach is worse
     * than a slightly smaller backdrop.
     */
    val MinSheetHeight: Dp = 240.dp

    /**
     * Box an ornament straddling the sheet's top edge is drawn in — today the order-detail mascot,
     * half over the sheet and half over the backdrop.
     *
     * Shared because the two apps had each written their own literal and drifted apart, 120 on the
     * customer against 128 on the partner. A difference that size is invisible on one phone and
     * obvious with both side by side, which is exactly the kind that survives a review.
     */
    val OrnamentSize: Dp = 128.dp

    val Shape: Shape = RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp)
}

@Stable
@OptIn(ExperimentalFoundationApi::class)
class SnapSheetState internal constructor(
    internal val draggable: AnchoredDraggableState<SnapAnchor>,
    internal val minSheetHeight: Dp,
    density: Density,
    private val stops: Set<SnapAnchor> = SnapAnchor.entries.toSet(),
) {
    private var containerHeightPx by mutableFloatStateOf(0f)
    private var minSheetHeightPx by mutableFloatStateOf(with(density) { minSheetHeight.toPx() })
    private var minSheetTopPx by mutableFloatStateOf(0f)
    private var mapFocusHeightPx by mutableStateOf<Float?>(null)

    val currentAnchor: SnapAnchor get() = draggable.currentValue

    /** Where the sheet will settle — already updated mid-drag, unlike [currentAnchor]. */
    val targetAnchor: SnapAnchor get() = draggable.targetValue

    /**
     * Y of the sheet's top edge. Falls back to the resting position of the
     * current anchor before the first layout resolves the live offset, so an
     * overlay glued to the edge (a mascot, a handle) doesn't render one frame
     * at the top of the screen.
     */
    val sheetTopPx: Float
        get() = draggable.offset.let { if (it.isNaN()) restingTopPx(currentAnchor) else it }

    suspend fun animateTo(anchor: SnapAnchor) {
        draggable.animateTo(anchor)
    }

    internal fun onLayout(containerHeightPx: Float, minSheetHeightPx: Float, minSheetTopPx: Float, mapFocusHeightPx: Float? = null) {
        this.containerHeightPx = containerHeightPx
        this.minSheetHeightPx = minSheetHeightPx
        this.minSheetTopPx = minSheetTopPx
        this.mapFocusHeightPx = mapFocusHeightPx
        draggable.updateAnchors(
            DraggableAnchors {
                SnapAnchor.entries.filter { it in stops }.forEach { anchor -> anchor at restingTopPx(anchor) }
            },
        )
    }

    private fun restingTopPx(anchor: SnapAnchor): Float = snapSheetTopPx(
        anchor = anchor,
        containerHeightPx = containerHeightPx,
        minSheetHeightPx = minSheetHeightPx,
        minSheetTopPx = minSheetTopPx,
        mapFocusHeightPx = mapFocusHeightPx,
    )

    internal fun nestedScrollConnection(): NestedScrollConnection = object : NestedScrollConnection {
        // Drag up first expands the sheet, and only once it is against its
        // deepest anchor does the content underneath start scrolling. Drag
        // down does the reverse via onPostScroll: the content scrolls back to
        // its top, then whatever is left over collapses the sheet. Same
        // hand-off Material's own bottom sheet implements.
        override fun onPreScroll(available: Offset, source: NestedScrollSource): Offset =
            if (available.y < 0f && source == NestedScrollSource.UserInput) {
                Offset(0f, draggable.dispatchRawDelta(available.y))
            } else {
                Offset.Zero
            }

        override fun onPostScroll(
            consumed: Offset,
            available: Offset,
            source: NestedScrollSource,
        ): Offset = if (source == NestedScrollSource.UserInput) {
            Offset(0f, draggable.dispatchRawDelta(available.y))
        } else {
            Offset.Zero
        }

        override suspend fun onPreFling(available: Velocity): Velocity {
            val offset = draggable.offset
            return if (available.y < 0f && !offset.isNaN() && offset > draggable.anchors.minPosition()) {
                draggable.settle(available.y)
                available
            } else {
                Velocity.Zero
            }
        }

        override suspend fun onPostFling(consumed: Velocity, available: Velocity): Velocity {
            draggable.settle(available.y)
            return available
        }
    }
}

@Composable
@OptIn(ExperimentalFoundationApi::class)
fun rememberSnapSheetState(
    initialAnchor: SnapAnchor = SnapAnchor.Peek,
    minSheetHeight: Dp = SnapSheetDefaults.MinSheetHeight,
    stops: Set<SnapAnchor> = SnapAnchor.entries.toSet(),
): SnapSheetState {
    val density = LocalDensity.current
    val decay = rememberSplineBasedDecay<Float>()
    return remember(density, minSheetHeight, stops) {
        SnapSheetState(
            draggable = AnchoredDraggableState(
                initialValue = initialAnchor,
                positionalThreshold = { distance -> distance * 0.4f },
                velocityThreshold = { with(density) { 500.dp.toPx() } },
                snapAnimationSpec = spring(dampingRatio = 0.9f, stiffness = 400f),
                decayAnimationSpec = decay,
            ),
            minSheetHeight = minSheetHeight,
            density = density,
            stops = stops,
        )
    }
}

/**
 * Non-modal sheet parked at one of three anchors over a full-bleed backdrop — the map layout.
 *
 * **Nothing here is ever dismissed**: the shallowest anchor still leaves the sheet on screen.
 * [minSheetTop] keeps the sheet's top at or under that line; [minSheetHeight] replaces the state's floor while the
 * host measures it, without resetting where the sheet rests. [mapFocusHeight] is the sheet's height at
 * [SnapAnchor.MapFocus] in place of its share of the container; null keeps the share.
 * -> /flows/offerability-and-take
 */
@Composable
@OptIn(ExperimentalFoundationApi::class)
fun SnapSheet(
    state: SnapSheetState,
    modifier: Modifier = Modifier,
    sheetShape: Shape = SnapSheetDefaults.Shape,
    sheetColor: Color = MaterialTheme.colorScheme.surface,
    minSheetTop: Dp = 0.dp,
    minSheetHeight: Dp? = null,
    mapFocusHeight: Dp? = null,
    backdrop: @Composable BoxScope.() -> Unit,
    overlay: @Composable BoxScope.() -> Unit = {},
    sheetContent: @Composable ColumnScope.() -> Unit,
) {
    BoxWithConstraints(modifier = modifier.fillMaxSize()) {
        val containerHeightPx = constraints.maxHeight.toFloat()
        val density = LocalDensity.current
        val floorPx = with(density) { (minSheetHeight ?: state.minSheetHeight).toPx() }
        val topPx = with(density) { minSheetTop.toPx() }
        val focusPx = mapFocusHeight?.let { with(density) { it.toPx() } }
        remember(state, containerHeightPx, floorPx, topPx, focusPx) {
            state.onLayout(containerHeightPx, floorPx, topPx, focusPx)
            containerHeightPx
        }
        val nestedScroll = remember(state) { state.nestedScrollConnection() }

        Box(modifier = Modifier.fillMaxSize(), content = backdrop)

        Column(
            modifier = Modifier
                .fillMaxWidth()
                .snapSheetPlacement(state)
                .shadow(12.dp, sheetShape)
                .clip(sheetShape)
                .background(sheetColor)
                .nestedScroll(nestedScroll)
                .anchoredDraggable(state.draggable, Orientation.Vertical),
            content = sheetContent,
        )

        Box(modifier = Modifier.fillMaxSize(), content = overlay)
    }
}

/**
 * Sizes the sheet to the space below its top edge and places it there, both in
 * the layout phase. Reading the drag offset here rather than in composition is
 * what keeps a drag from recomposing the whole sheet on every frame.
 */
private fun Modifier.snapSheetPlacement(state: SnapSheetState) = layout { measurable, constraints ->
    if (!constraints.hasBoundedHeight) {
        val placeable = measurable.measure(constraints)
        return@layout layout(placeable.width, placeable.height) { placeable.place(0, 0) }
    }
    val container = constraints.maxHeight
    val top = state.sheetTopPx.roundToInt().coerceIn(0, container)
    val height = container - top
    val placeable = measurable.measure(constraints.copy(minHeight = height, maxHeight = height))
    layout(placeable.width, container) { placeable.place(0, top) }
}
