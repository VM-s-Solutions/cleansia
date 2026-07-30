package cz.cleansia.customer.features.addresses

import androidx.activity.compose.BackHandler
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
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.core.data.UserAddress
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.drop
import kotlin.math.roundToInt

private enum class SheetAnchor { Hidden, Full }

/**
 * Bottom-overlay wrapper around [AddressManagerScreen] — mirrors the booking
 * sheet's slide-up + anchored-drag feel, so the address manager opens from the
 * bottom when the home top-bar is tapped.
 */
@OptIn(ExperimentalFoundationApi::class)
@Composable
fun AddressManagerSheet(
    visible: Boolean,
    onDismiss: () -> Unit = {},
    onAddressSelected: (UserAddress) -> Unit = {},
) {
    BoxWithConstraints(modifier = Modifier.fillMaxSize()) {
        val parentHeightPx = constraints.maxHeight.toFloat()

        SheetWithAnchors(
            parentHeightPx = parentHeightPx,
            visible = visible,
            onDismiss = onDismiss,
            onAddressSelected = onAddressSelected,
        )
    }
}

@OptIn(ExperimentalFoundationApi::class)
@Composable
private fun SheetWithAnchors(
    parentHeightPx: Float,
    visible: Boolean,
    onDismiss: () -> Unit,
    onAddressSelected: (UserAddress) -> Unit,
) {
    val density = LocalDensity.current
    val decay = rememberSplineBasedDecay<Float>()

    val draggableState = remember(parentHeightPx) {
        AnchoredDraggableState(
            initialValue = SheetAnchor.Hidden,
            positionalThreshold = { distance -> distance * 0.4f },
            velocityThreshold = { with(density) { 500.dp.toPx() } },
            snapAnimationSpec = spring(dampingRatio = 0.9f, stiffness = 400f),
            decayAnimationSpec = decay,
        ).apply {
            updateAnchors(
                DraggableAnchors {
                    SheetAnchor.Hidden at parentHeightPx
                    SheetAnchor.Full at parentHeightPx * 0.08f
                },
            )
        }
    }

    // Keyed on parentHeightPx as well as visible, because `remember(parentHeightPx)`
    // above rebuilds the state at `Hidden` whenever the constraint changes — the IME
    // opening over any of AddressManagerScreen's six text fields is enough under
    // windowSoftInputMode="adjustResize". Keyed on `visible` alone this effect would
    // not re-run, leaving an open sheet parked off-screen with the caller's
    // `addressSheetOpen` still true; re-tapping then assigns true over true, which is
    // structurally equal, so nothing recomposes and the panel is dead for the session.
    // Defensive, not the reported bug: the device test cleared this path (opening the
    // keyboard and closing normally still reopened fine). Kept because the failure mode
    // is identical and unrecoverable, and the extra key costs one comparison.
    LaunchedEffect(visible, parentHeightPx) {
        if (!visible) {
            draggableState.animateTo(SheetAnchor.Hidden)
            return@LaunchedEffect
        }
        draggableState.animateTo(SheetAnchor.Full)
        snapshotFlow { draggableState.currentValue }
            .distinctUntilChanged()
            // THIS is the line that fixed the reported bug — confirmed on device: the
            // panel died after a hard fling-down dismiss, not after a back press or an
            // IME resize. Without the drop, the value emitted on subscribe is already
            // Hidden after a fling, so onDismiss fires immediately on the NEXT open and
            // shuts the sheet before it is seen — with the caller's flag left true, so
            // re-tapping assigns true over true and nothing recomposes again.
            // Aligns with BookingBottomSheet, which has always had it.
            .drop(1)
            .collect { value ->
                if (value == SheetAnchor.Hidden) onDismiss()
            }
    }

    // Without this, system back reaches the nav host underneath and pops the screen
    // hosting the sheet while `addressSheetOpen` stays true — the same true-over-true
    // dead end as above. Routed through onDismiss so back and the header arrow leave
    // identical state. Also not the reported bug (the back-arrow test passed), but back
    // dismissing a sheet is the platform expectation and its absence was real.
    BackHandler(enabled = visible) { onDismiss() }

    if (!visible && draggableState.currentValue == SheetAnchor.Hidden) return

    val offsetPx = if (draggableState.offset.isNaN()) parentHeightPx else draggableState.offset
    val fullAnchor = parentHeightPx * 0.08f
    val sheetHeightDp = with(LocalDensity.current) { (parentHeightPx - fullAnchor).toDp() }

    // Drag is enabled everywhere except on the map pane — the map needs full
    // control of vertical pans. AddressManagerScreen flips this via its callback.
    var mapActive by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .height(sheetHeightDp)
            .offset { IntOffset(0, offsetPx.roundToInt()) }
            .anchoredDraggable(
                state = draggableState,
                orientation = Orientation.Vertical,
                enabled = !mapActive,
            )
            .shadow(
                elevation = 28.dp,
                shape = RoundedCornerShape(topStart = 20.dp, topEnd = 20.dp),
                clip = false,
                ambientColor = Color.Black,
                spotColor = Color.Black,
            )
            .background(
                color = MaterialTheme.colorScheme.background,
                shape = RoundedCornerShape(topStart = 20.dp, topEnd = 20.dp),
            ),
    ) {
        // Drag handle pill — hidden when drag is disabled (map pane) since the
        // affordance would be misleading.
        if (!mapActive) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp, bottom = 4.dp),
                contentAlignment = Alignment.TopCenter,
            ) {
                Box(
                    modifier = Modifier
                        .size(width = 32.dp, height = 4.dp)
                        .background(
                            MaterialTheme.colorScheme.onSurface.copy(alpha = 0.12f),
                            RoundedCornerShape(2.dp),
                        ),
                )
            }
        }

        AddressManagerScreen(
            onBack = onDismiss,
            onAddressSelected = onAddressSelected,
            onMapActiveChanged = { mapActive = it },
            isInSheet = true,
        )
    }
}
