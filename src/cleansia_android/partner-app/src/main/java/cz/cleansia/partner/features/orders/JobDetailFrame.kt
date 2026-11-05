package cz.cleansia.partner.features.orders

import android.provider.Settings
import androidx.annotation.DrawableRes
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.windowInsetsBottomHeight
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.Composable
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.scale
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.FloatingCircleButton
import cz.cleansia.core.ui.components.MascotAnimation
import cz.cleansia.core.ui.components.MascotPuck
import cz.cleansia.core.ui.components.MascotPuckScale
import cz.cleansia.core.ui.components.SnapAnchor
import cz.cleansia.core.ui.components.SnapSheet
import cz.cleansia.core.ui.components.rememberSnapSheetState
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import kotlin.math.roundToInt
import kotlinx.coroutines.launch

/**
 * The job detail's page: the core snap sheet over the band, which holds the map (or the band's colour) and never moves,
 * the back button fixed over it, the state's figure on the panel's top edge and the state's one move pinned under the
 * thumb. The panel rests expanded at the band's foot; dragged down, or its handle tapped, it collapses onto [head],
 * which stays pinned while the rest scrolls inside the panel, and ends just under it, above the move or the system bar. At large text the figure hides, the band keeps only the
 * back button's room and the head scrolls with the rest. [map] draws behind everything at the band's scale, given the
 * band's height and the panel's live top; [cleaning] plays the cleaning loop in the figure's place, its first frame
 * when animations are off.
 */
@Composable
internal fun JobDetailFrame(
    onNavigateBack: () -> Unit,
    @DrawableRes mascot: Int?,
    pinned: (@Composable () -> Unit)?,
    spokenTitle: String? = null,
    map: (@Composable (band: Dp, sheetTopPx: () -> Float) -> Unit)? = null,
    cleaning: Boolean = false,
    head: (@Composable (PuckClearance) -> Unit)? = null,
    content: @Composable ColumnScope.(PuckClearance) -> Unit,
) {
    val density = LocalDensity.current
    val large = density.fontScale >= 1.3f
    val figure = mascot.takeIf { !large }
    val context = LocalContext.current
    val still = remember(context) {
        Settings.Global.getFloat(context.contentResolver, Settings.Global.ANIMATOR_DURATION_SCALE, 1f) == 0f
    }
    val band = WindowInsets.statusBars.asPaddingValues().calculateTopPadding() + when {
        large -> BandAtLargeText
        map != null -> BandWithMap
        else -> BandBody
    }
    val pinsHead = head != null && !large
    var pinnedHeight by remember { mutableStateOf(0.dp) }
    val peek = if (pinsHead) collapsedHeight(pinnedHeight) else null
    val sheet = rememberSnapSheetState(initialAnchor = SnapAnchor.Expanded, stops = PanelStops)
    val panelScroll = rememberScrollState()
    val scrolled by remember { derivedStateOf { panelScroll.value > 0 } }
    val scope = rememberCoroutineScope()
    val clearance = figure?.let { PuckClearance(width = PuckClearWidth, foot = puckFrameTop(it).let { top -> PuckSize - top } - SheetPadding) } ?: PuckClearance.None

    Column(Modifier.fillMaxSize().background(MaterialTheme.colorScheme.surface)) {
        SnapSheet(
            state = sheet,
            modifier = Modifier.weight(1f).fillMaxWidth(),
            minSheetTop = band - SheetOverBand,
            minSheetHeight = peek ?: 0.dp,
            mapFocusHeight = peek,
            backdrop = {
                Box(Modifier.fillMaxSize().background(MaterialTheme.colorScheme.primaryContainer))
                map?.let { draw -> draw(band) { sheet.sheetTopPx } }
            },
            overlay = {
                figure?.let { art ->
                    val place = Modifier
                        .align(Alignment.TopEnd)
                        .padding(end = 16.dp)
                        .offset { IntOffset(0, (sheet.sheetTopPx - puckFrameTop(art).toPx()).roundToInt()) }
                    if (cleaning) {
                        MascotAnimation(
                            resId = R.raw.mascot_cleaning_in_progress,
                            size = PuckSize,
                            still = still,
                            modifier = place.scale(MascotPuckScale.CleaningLoop),
                        )
                    } else {
                        MascotPuck(painter = painterResource(art), scale = puckScale(art), size = PuckSize, modifier = place)
                    }
                }
                Box(Modifier.windowInsetsPadding(WindowInsets.statusBars)) {
                    FloatingCircleButton(
                        icon = Icons.AutoMirrored.Outlined.ArrowBack,
                        contentDescription = stringResource(R.string.back),
                        onClick = onNavigateBack,
                        modifier = Modifier.padding(start = 16.dp, top = BackTop),
                    )
                    spokenTitle?.let { title -> Box(Modifier.size(1.dp).semantics { heading(); contentDescription = title }) }
                }
            },
        ) {
            Column(Modifier.fillMaxWidth().onSizeChanged { pinnedHeight = with(density) { it.height.toDp() } }) {
                PanelHandle {
                    scope.launch { sheet.animateTo(if (sheet.targetAnchor == SnapAnchor.MapFocus) SnapAnchor.Expanded else SnapAnchor.MapFocus) }
                }
                if (pinsHead) Box(Modifier.padding(horizontal = SheetPadding)) { head?.invoke(clearance) }
            }
            Box(Modifier.fillMaxWidth().weight(1f)) {
                Column(
                    Modifier
                        .fillMaxSize()
                        .verticalScroll(panelScroll)
                        .padding(start = SheetPadding, end = SheetPadding, bottom = if (pinned != null) 16.dp else 40.dp),
                ) {
                    if (head != null && !pinsHead) head(clearance)
                    content(if (head != null) PuckClearance.None else clearance)
                }
                if (pinsHead && scrolled) HeadEdge()
            }
        }
        if (pinned == null) {
            Spacer(Modifier.fillMaxWidth().background(MaterialTheme.colorScheme.surface).windowInsetsBottomHeight(WindowInsets.navigationBars))
        } else {
            Surface(color = MaterialTheme.colorScheme.surface, shadowElevation = 8.dp, modifier = Modifier.fillMaxWidth()) {
                Column {
                    if (isDarkScheme()) HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                    Box(
                        Modifier
                            .fillMaxWidth()
                            .padding(start = 16.dp, end = 16.dp, top = 12.dp, bottom = 12.dp)
                            .windowInsetsPadding(WindowInsets.navigationBars),
                    ) { pinned() }
                }
            }
        }
    }
}

/**
 * Once the rest has scrolled, the head reaches 8 further on the panel's surface and ends in the footer's rule, so a line
 * going under it is cut at a rule, never left as a sliver under the head's last line.
 */
@Composable
private fun HeadEdge() {
    Box(Modifier.fillMaxWidth().height(HeadEdgeHeight).background(MaterialTheme.colorScheme.surface)) {
        HorizontalDivider(Modifier.align(Alignment.BottomCenter), color = MaterialTheme.colorScheme.outlineVariant)
    }
}

/** The panel's grabber in its top padding: a tap toggles collapsed and expanded, a drag moves the panel. */
@Composable
private fun PanelHandle(onTap: () -> Unit) {
    Box(
        Modifier
            .fillMaxWidth()
            .height(SheetPadding)
            .clickable(interactionSource = null, indication = null, onClick = onTap)
            .clearAndSetSemantics {},
        contentAlignment = Alignment.TopCenter,
    ) {
        Box(
            Modifier
                .padding(top = 6.dp)
                .size(width = 36.dp, height = 4.dp)
                .background(MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(2.dp)),
        )
    }
}

/**
 * The collapsed panel's height, ending clean under the head: the handle and the head as measured, and the 8 the head's
 * edge takes once the rest has scrolled, which the rest's first gap leaves empty at rest. Unmeasured, the sheet's share.
 */
internal fun collapsedHeight(pinned: Dp): Dp? = if (pinned > 0.dp) pinned + HeadEdgeHeight else null

/** How far above the sheet's edge the figure's frame starts: centred on the edge, or the waving figure's ledge on it. */
private fun puckFrameTop(@DrawableRes art: Int): Dp =
    if (art == R.drawable.mascot_waving) PuckSize * MascotPuckScale.WavingLedge else PuckSize / 2

internal fun puckScale(@DrawableRes art: Int): Float = when (art) {
    R.drawable.mascot_waving -> MascotPuckScale.Waving
    R.drawable.mascot_ready -> MascotPuckScale.Ready
    R.drawable.mascot_resting -> MascotPuckScale.Resting
    R.drawable.mascot_spray_and_cloth -> MascotPuckScale.SprayAndCloth
    R.drawable.mascot_thumbs_up -> MascotPuckScale.ThumbsUp
    R.drawable.mascot_invoice -> MascotPuckScale.Invoice
    else -> MascotPuckScale.Leaning
}

private val PanelStops = setOf(SnapAnchor.MapFocus, SnapAnchor.Expanded)

private val PuckSize = 128.dp
private val PuckClearWidth = 120.dp
private val SheetPadding = 16.dp
internal val SheetOverBand = 24.dp
private val HeadEdgeHeight = Spacing.XS
private val BackTop = 12.dp
private val BandBody = 96.dp + SheetOverBand
private val BandAtLargeText = 68.dp + SheetOverBand
private val BandWithMap = 136.dp + SheetOverBand
