package cz.cleansia.partner.features.orders

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.requiredSize
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.AutoAwesome
import androidx.compose.material.icons.outlined.ErrorOutline
import androidx.compose.material.icons.outlined.Group
import androidx.compose.material.icons.outlined.Payments
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.AlignmentLine
import androidx.compose.ui.layout.FirstBaseline
import androidx.compose.ui.layout.Layout
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.rememberTextMeasurer
import androidx.compose.ui.text.style.LineBreak
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.CleansiaButtonSize
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.core.ui.theme.capBandAligned
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.data.orders.PendingOffer
import cz.cleansia.partner.ui.theme.Green400
import cz.cleansia.partner.ui.theme.SuccessText
import cz.cleansia.partner.ui.theme.pendingInk
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

/**
 * An open job's row: the window with its pay trailing, the area, the scope and the flags, and a tinted chevron column
 * on the card's trailing edge. The whole row is one button that opens the job; taking happens on the detail.
 */
@Composable
fun AvailableJobRow(
    row: OrderListItem,
    now: Instant,
    clash: OrderListItem?,
    onOpen: () -> Unit,
    modifier: Modifier = Modifier,
    bodyFocus: FocusRequester? = null,
    bottomPadding: Dp = Spacing.M,
) {
    val locale = appLocale()
    val zone = ZoneId.systemDefault()
    val rules = OrdersBoardRules
    val start = rules.start(row)
    val window = rules.window(start, rules.minutes(row), zone, locale)
    val pay = rules.payText(row, locale)
    val team = (row.requiredEmployees ?: 1) > 1
    val area = rules.areaLine(row)
    val band = rules.bandText(row.homeDistanceBand)?.text()
    val scope = rules.scopeItems(row, locale.language).map { it.text() }
    val underWay = rules.liveFlag(row, now) is LiveFlag.UnderWay
    val clashText = clash?.let {
        rules.tie(stringResource(R.string.orders_flag_overlap, rules.window(rules.start(it), rules.minutes(it), zone, locale).orEmpty()))
    }
    val flags = rowFlags(row, now, clashText)
    val dayText = start?.let { dayAndDate(it, now) }
    val caption = stringResource(if (team) R.string.orders_card_your_share else R.string.you_earn)
    val sentence = spokenSentence(
        emptyList(),
        listOf(dayText, window, stringResource(R.string.orders_flag_under_way_short).takeIf { underWay }, pay?.let { "$caption $it" }),
        listOf(area, band),
        scope,
        *flags.map { listOf(it.spoken) }.toTypedArray(),
    )
    val large = LocalDensity.current.fontScale >= 1.3f
    val scheme = MaterialTheme.colorScheme
    val openLabel = stringResource(R.string.orders_open_job)
    val payText: @Composable (Modifier) -> Unit = { m ->
        if (pay != null) {
            Text(
                text = pay,
                style = MaterialTheme.typography.titleMedium.copy(fontFeatureSettings = "tnum"),
                color = scheme.onSurface,
                softWrap = false,
                maxLines = 1,
                modifier = m,
            )
        }
    }
    Box(
        modifier = modifier
            .fillMaxWidth()
            .then(if (bodyFocus != null) Modifier.focusRequester(bodyFocus) else Modifier)
            .clickable(onClickLabel = openLabel, role = Role.Button, onClick = onOpen)
            .clearAndSetSemantics { contentDescription = sentence },
    ) {
        Column(
            Modifier
                .fillMaxWidth()
                .padding(start = Spacing.M, end = ChevronColumnWidth + Spacing.S, top = Spacing.S, bottom = bottomPadding),
        ) {
            if (large) {
                WindowLine(window = window ?: "—", underWay = underWay)
                payText(Modifier.padding(top = 2.dp))
            } else {
                Row {
                    Box(Modifier.weight(1f).alignBy(FirstBaseline)) { WindowLine(window = window ?: "—", underWay = underWay) }
                    payText(Modifier.padding(start = Spacing.S).alignBy(FirstBaseline))
                }
            }
            DotFlow(
                items = listOfNotNull(area?.let { AnnotatedString(it) }, band?.let { styled(it, SpanStyle(color = scheme.onSurfaceVariant)) }),
                style = MaterialTheme.typography.bodyMedium,
                color = scheme.onSurface,
                modifier = Modifier.padding(top = 2.dp),
            )
            DotFlow(
                items = scope.map { AnnotatedString(it) },
                style = MaterialTheme.typography.bodyMedium,
                color = scheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 8.dp),
                tieLast = true,
            )
            if (flags.isNotEmpty()) FlagFlow(Modifier.padding(top = 10.dp)) { flags.forEach { it.draw() } }
        }
        Box(Modifier.matchParentSize(), contentAlignment = Alignment.CenterEnd) { ChevronEdge() }
    }
}

private val ChevronColumnWidth = 16.dp

/** The open row's trailing edge: a hairline, the faintest primary tint and the chevron centred; the skeleton's has no chevron. */
@Composable
internal fun ChevronEdge(showsChevron: Boolean = true) {
    val scheme = MaterialTheme.colorScheme
    Box(
        Modifier
            .fillMaxHeight()
            .width(ChevronColumnWidth)
            .background(scheme.primary.copy(alpha = 0.05f))
            .drawBehind {
                val x = if (layoutDirection == LayoutDirection.Rtl) size.width - 1f else 0f
                drawRect(scheme.outline, topLeft = Offset(x, 0f), size = Size(1f, size.height))
            },
        contentAlignment = Alignment.Center,
    ) {
        if (showsChevron) {
            Icon(
                imageVector = Icons.AutoMirrored.Outlined.KeyboardArrowRight,
                contentDescription = null,
                tint = scheme.primary,
                modifier = Modifier.requiredSize(20.dp),
            )
        }
    }
}

/**
 * The reserved card's row: its first line names the day, its tile says Confirm, and its scope sits beside the tile
 * unless full width is shorter. [tileWidth] is the page's one tile column. The body layer under the content opens the
 * detail and speaks one sentence; the tile is a sibling button naming its job.
 */
@Composable
private fun ReservedJobRow(
    row: OrderListItem,
    now: Instant,
    clash: OrderListItem?,
    onOpen: () -> Unit,
    onConfirm: () -> Unit,
    extraSentence: List<String>,
    tileWidth: Dp?,
    bottomPadding: Dp,
) {
    val locale = appLocale()
    val zone = ZoneId.systemDefault()
    val rules = OrdersBoardRules
    val start = rules.start(row)
    val window = rules.window(start, rules.minutes(row), zone, locale)
    val pay = rules.payText(row, locale)
    val team = (row.requiredEmployees ?: 1) > 1
    val area = rules.areaLine(row)
    val band = rules.bandText(row.homeDistanceBand)?.text()
    val scope = rules.scopeItems(row, locale.language).map { it.text() }
    val underWay = rules.liveFlag(row, now) is LiveFlag.UnderWay
    val clashText = clash?.let {
        rules.tie(stringResource(R.string.orders_flag_overlap, rules.window(rules.start(it), rules.minutes(it), zone, locale).orEmpty()))
    }
    val flags = rowFlags(row, now, clashText)
    val dayText = start?.let { dayAndDate(it, now) }
    val dayWord = start?.let { rules.dayName(it.atZone(zone).toLocalDate(), now.atZone(zone).toLocalDate(), locale).text() }
    val action = stringResource(R.string.offer_confirm)
    val caption = stringResource(if (team) R.string.orders_card_your_share else R.string.you_earn)
    val spec = reservedTileSpec(row)
    val sentence = spokenSentence(
        extraSentence,
        listOf(dayText, window, stringResource(R.string.orders_flag_under_way_short).takeIf { underWay }, pay?.let { "$caption $it" }),
        listOf(area, band),
        scope,
        *flags.map { listOf(it.spoken) }.toTypedArray(),
    )
    val large = LocalDensity.current.fontScale >= 1.3f
    val ownTileWidth = tileWidth ?: payTileWidth(listOf(spec))
    val scheme = MaterialTheme.colorScheme
    Box(modifier = Modifier.fillMaxWidth()) {
        Box(
            modifier = Modifier
                .matchParentSize()
                .semantics(mergeDescendants = true) { contentDescription = sentence }
                .clickable(onClickLabel = stringResource(R.string.orders_open_job), onClick = onOpen),
        )
        val head: @Composable () -> Unit = {
            Column(Modifier.clearAndSetSemantics {}) {
                DotFlow(
                    items = listOfNotNull(dayWord?.let { AnnotatedString(it) }, AnnotatedString(window ?: "—")),
                    style = MaterialTheme.typography.bodyLarge.copy(fontFeatureSettings = "tnum"),
                    color = scheme.onSurface,
                )
                DotFlow(
                    items = listOfNotNull(area?.let { AnnotatedString(it) }, band?.let { styled(it, SpanStyle(color = scheme.onSurfaceVariant)) }),
                    style = MaterialTheme.typography.bodyMedium,
                    color = scheme.onSurface,
                    modifier = Modifier.padding(top = 2.dp),
                )
            }
        }
        val tileContent: @Composable () -> Unit = {
            PayTile(
                spec = spec,
                description = actionLabel(action, dayWord?.lowercase(locale), window),
                wide = large,
                onClick = onConfirm,
            )
        }
        val scopeBlock: @Composable () -> Unit = {
            DotFlow(
                items = scope.map { AnnotatedString(it) },
                style = MaterialTheme.typography.bodyMedium,
                color = scheme.onSurfaceVariant,
                modifier = Modifier.clearAndSetSemantics {},
                tieLast = true,
            )
        }
        val flagsBlock: @Composable () -> Unit = {
            if (flags.isNotEmpty()) FlagFlow(Modifier.clearAndSetSemantics {}) { flags.forEach { it.draw() } }
        }
        JobRowLayout(
            head = head,
            tile = tileContent,
            scope = scopeBlock,
            flags = flagsBlock,
            tileWidth = ownTileWidth,
            oneColumn = large,
            modifier = Modifier.padding(start = Spacing.M, end = Spacing.M, top = Spacing.S, bottom = bottomPadding),
        )
    }
}

/**
 * `[text 1fr] [tile]` top-aligned, the scope beside the tile unless full width makes the row shorter (measured, as
 * ViewThatFits would), and the flags at the foot. At large text one column, the tile a full-width bar last.
 */
@Composable
private fun JobRowLayout(
    head: @Composable () -> Unit,
    tile: @Composable () -> Unit,
    scope: @Composable () -> Unit,
    flags: @Composable () -> Unit,
    tileWidth: Dp,
    oneColumn: Boolean,
    modifier: Modifier = Modifier,
) {
    // The scope is composed twice, once per candidate placement, so each copy is measured once; one is placed.
    Layout(contents = listOf(head, tile, scope, scope, flags), modifier = modifier) { (headM, tileM, scopeM, scopeWideM, flagsM), constraints ->
        val width = constraints.maxWidth
        val gap = 12.dp.roundToPx()
        val line = 8.dp.roundToPx()
        val flagGap = 10.dp.roundToPx()
        val loose = Constraints(maxWidth = width)
        if (oneColumn) {
            val headP = headM.first().measure(loose)
            val scopeP = scopeM.firstOrNull()?.measure(loose)?.takeIf { it.height > 0 }
            val flagsP = flagsM.firstOrNull()?.measure(loose)?.takeIf { it.height > 0 }
            val tileP = tileM.first().measure(Constraints.fixedWidth(width))
            val parts = listOfNotNull(headP to 0, scopeP?.let { it to line }, flagsP?.let { it to flagGap }, tileP to gap)
            val height = parts.sumOf { (p, above) -> p.height + above }
            return@Layout layout(width, height) {
                var y = 0
                parts.forEach { (p, above) ->
                    y += above
                    p.place(0, y)
                    y += p.height
                }
            }
        }
        val tileP = tileM.first().measure(Constraints.fixedWidth(minOf(tileWidth.roundToPx(), width)))
        val textWidth = (width - tileP.width - gap).coerceAtLeast(0)
        val headP = headM.first().measure(Constraints(maxWidth = textWidth))
        val scopeBeside = scopeM.firstOrNull()?.measure(Constraints(maxWidth = textWidth))
        val scopeWide = scopeWideM.firstOrNull()?.measure(loose)
        val besideHeight = maxOf(headP.height + (scopeBeside?.let { line + it.height } ?: 0), tileP.height)
        val wideHeight = maxOf(headP.height, tileP.height) + (scopeWide?.let { line + it.height } ?: 0)
        val wide = scopeWide != null && OrdersBoardRules.scopeFullWidth(besideHeight, wideHeight, measured = true)
        val top = if (wide) wideHeight else besideHeight
        val flagsP = flagsM.firstOrNull()?.measure(loose)?.takeIf { it.height > 0 }
        val height = top + (flagsP?.let { flagGap + it.height } ?: 0)
        layout(width, height) {
            headP.place(0, 0)
            tileP.place(width - tileP.width, 0)
            if (wide) scopeWide?.place(0, maxOf(headP.height, tileP.height) + line) else scopeBeside?.place(0, headP.height + line)
            flagsP?.place(0, top + flagGap)
        }
    }
}

/** The window with *Under way* beside it once the job has started, under it when the two do not fit one line. */
@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun WindowLine(window: String, underWay: Boolean) {
    val caption = MaterialTheme.typography.bodySmall
    FlowRow(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
        Text(
            text = window,
            style = MaterialTheme.typography.bodyLarge.copy(fontFeatureSettings = "tnum"),
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.alignByBaseline(),
        )
        if (underWay) {
            Row(modifier = Modifier.alignByBaseline()) {
                Box(xBandAligned(caption).size(8.dp).background(if (isDarkScheme()) Green400 else SuccessText, CircleShape))
                Text(
                    text = stringResource(R.string.orders_flag_under_way_short),
                    style = caption,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(start = 6.dp).alignBy(FirstBaseline),
                    softWrap = false,
                )
            }
        }
    }
}

internal fun styled(text: String, style: SpanStyle): AnnotatedString = buildAnnotatedString { withStyle(style) { append(text) } }

/** One tile's words: what the page's tile column must hold. */
internal data class TileSpec(val pay: String?, val action: String)

/** A reserved row's tile: its pay over Confirm. */
@Composable
internal fun reservedTileSpec(row: OrderListItem): TileSpec =
    TileSpec(pay = OrdersBoardRules.payText(row, appLocale()), action = stringResource(R.string.offer_confirm))

/** The page's one tile column: the widest tile's need, at least 108. */
@Composable
internal fun payTileWidth(specs: List<TileSpec>): Dp {
    val measurer = rememberTextMeasurer()
    val density = LocalDensity.current
    val figure = MaterialTheme.typography.titleMedium.copy(fontFeatureSettings = "tnum")
    val action = MaterialTheme.typography.labelLarge
    val width = { text: String, style: TextStyle -> measurer.measure(text, style, maxLines = 1, softWrap = false).size.width }
    val need = specs.maxOfOrNull { spec ->
        val act = width(spec.action, action) + with(density) { (2.dp + glyphSize(action)).roundToPx() }
        maxOf(spec.pay?.let { width(it, figure) } ?: 0, act)
    } ?: 0
    return maxOf(TileMinWidth, with(density) { need.toDp() } + TilePadding * 2)
}

private val TileMinWidth = 108.dp
private val TilePadding = 12.dp

/** A glyph beside a line of [style]: 16 at a 14 label, growing with the text as the label does. */
@Composable
internal fun glyphSize(style: TextStyle): Dp = with(LocalDensity.current) { style.fontSize.toDp() } * 8 / 7

/**
 * A tile's one move: pay over its action, figure and action centred on the tile's one axis, in the app's tint. [wide]
 * lays it out as a full-width bar, figure leading and action trailing on one line while they fit, else the action
 * trailing on the next (large text).
 */
@Composable
internal fun PayTile(
    spec: TileSpec,
    description: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    wide: Boolean = false,
) {
    val scheme = MaterialTheme.colorScheme
    val actionInk = primaryText()
    Surface(
        onClick = onClick,
        shape = MaterialTheme.shapes.small,
        color = tonalFill(),
        modifier = modifier.semantics {
            contentDescription = description
            role = Role.Button
        },
    ) {
        val figure: @Composable () -> Unit = {
            if (spec.pay != null) {
                Text(
                    text = spec.pay,
                    style = MaterialTheme.typography.titleMedium.copy(fontFeatureSettings = "tnum"),
                    color = scheme.onSurface,
                    softWrap = false,
                    maxLines = 1,
                )
            }
        }
        val actionRow: @Composable () -> Unit = {
            val style = MaterialTheme.typography.labelLarge
            Row {
                Text(text = spec.action, style = style, color = actionInk, maxLines = 1, softWrap = false, modifier = Modifier.alignBy(FirstBaseline))
                Icon(
                    imageVector = Icons.AutoMirrored.Outlined.KeyboardArrowRight,
                    contentDescription = null,
                    tint = actionInk,
                    modifier = capBandAligned(style).padding(start = 2.dp).size(glyphSize(style)),
                )
            }
        }
        if (wide) {
            Layout(
                contents = listOf(figure, actionRow),
                modifier = Modifier.heightIn(min = 48.dp).padding(horizontal = Spacing.M, vertical = 10.dp),
            ) { (lead, action), constraints ->
                val loose = constraints.copy(minWidth = 0, minHeight = 0)
                val act = action.first().measure(loose)
                val gap = Spacing.S.roundToPx()
                val width = constraints.maxWidth
                val oneLine = lead.first().maxIntrinsicWidth(Constraints.Infinity) + gap + act.width <= width
                val head = lead.first().measure(if (oneLine) loose.copy(maxWidth = width - act.width - gap) else loose)
                val height = (if (oneLine) maxOf(head.height, act.height) else head.height + act.height).coerceAtLeast(constraints.minHeight)
                layout(width, height) {
                    if (oneLine) {
                        head.place(0, (height - head.height) / 2)
                        act.place(width - act.width, (height - act.height) / 2)
                    } else {
                        head.place(0, 0)
                        act.place(width - act.width, head.height)
                    }
                }
            }
        } else {
            Column(
                modifier = Modifier.padding(horizontal = TilePadding, vertical = Spacing.XS),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                figure()
                actionRow()
            }
        }
    }
}

/**
 * The separator flow: items never break inside and the dot stands only between two items of one
 * line, never at a line's start or end. Items of different sizes share their line's baseline.
 * [tieLast] never lets the last item start a line alone.
 */
@Composable
internal fun DotFlow(
    items: List<AnnotatedString>,
    style: TextStyle,
    color: Color,
    modifier: Modifier = Modifier,
    dotColor: Color = MaterialTheme.colorScheme.onSurfaceVariant,
    tieLast: Boolean = false,
) {
    if (items.isEmpty()) return
    val slot = with(LocalDensity.current) { (style.fontSize.toPx() * 14f / 15f).let { if (it.isNaN()) 14.dp.toPx() else it } }
    Layout(
        contents = listOf(
            { items.forEach { Text(text = it, style = style, color = color) } },
            { repeat(items.size - 1) { Text(text = "·", style = style, color = dotColor) } },
        ),
        modifier = modifier,
    ) { (itemMs, dotMs), constraints ->
        val width = constraints.maxWidth
        val slotPx = slot.toInt()
        val placeables = itemMs.map { it.measure(Constraints(maxWidth = width)) }
        val dots = dotMs.map { it.measure(Constraints()) }
        val lines = OrdersBoardRules.flowLines(placeables.map { it.width }, slotPx, width, tieLast)
        var y = 0
        val positions = mutableListOf<Triple<Int, Int, Int>>()
        val dotPositions = mutableListOf<Pair<Int, Int>>()
        var dotIndex = 0
        var usedWidth = 0
        lines.forEach { line ->
            val baseline = line.maxOf { placeables[it][FirstBaseline].takeIf { b -> b != AlignmentLine.Unspecified } ?: 0 }
            var x = 0
            var bottom = 0
            line.forEachIndexed { i, index ->
                val p = placeables[index]
                if (i > 0) {
                    val dot = dots[dotIndex++]
                    val dotBase = dot[FirstBaseline].takeIf { it != AlignmentLine.Unspecified } ?: 0
                    dotPositions += (x + (slotPx - dot.width) / 2) to (y + baseline - dotBase)
                    x += slotPx
                }
                val itemBase = p[FirstBaseline].takeIf { it != AlignmentLine.Unspecified } ?: 0
                val top = y + baseline - itemBase
                positions += Triple(index, x, top)
                bottom = maxOf(bottom, top + p.height)
                x += p.width
            }
            usedWidth = maxOf(usedWidth, x)
            y = bottom
        }
        val layoutWidth = if (constraints.hasBoundedWidth) usedWidth.coerceIn(constraints.minWidth, width) else usedWidth
        layout(layoutWidth, y) {
            positions.forEach { (index, x, top) -> placeables[index].place(x, top) }
            dotPositions.forEachIndexed { i, (x, top) -> dots[i].place(x, top) }
        }
    }
}

/** A flag on a row: what TalkBack says of it, and how it is drawn. */
internal class RowFlag(val spoken: String, val draw: @Composable () -> Unit)

internal enum class FlagTone { Neutral, Warn, Negative }

@Composable
internal fun flagInk(tone: FlagTone): Color = when (tone) {
    FlagTone.Neutral -> MaterialTheme.colorScheme.onSurfaceVariant
    FlagTone.Warn -> MaterialTheme.colorScheme.pendingInk
    FlagTone.Negative -> MaterialTheme.colorScheme.error
}

/** Overlap → take-until → cash → crew → dirt; *Under way* sits on the window's line instead. */
@Composable
private fun rowFlags(row: OrderListItem, now: Instant, clashText: String?): List<RowFlag> {
    val rules = OrdersBoardRules
    val locale = appLocale()
    val zone = ZoneId.systemDefault()
    val flags = mutableListOf<RowFlag>()
    clashText?.let { text -> flags += RowFlag(text) { OrderFlag(text, FlagTone.Negative, Icons.Outlined.ErrorOutline) } }
    (rules.liveFlag(row, now) as? LiveFlag.TakeUntil)?.let { live ->
        val text = stringResource(R.string.orders_flag_take_until, rules.timeText(live.until, zone, locale))
        flags += RowFlag(text) { OrderFlag(text, FlagTone.Neutral, Icons.Outlined.Schedule) }
    }
    rules.cashFlag(row, locale)?.text()?.let { text -> flags += RowFlag(text) { OrderFlag(text, FlagTone.Warn, Icons.Outlined.Payments) } }
    rules.teamFlag(row)?.let { team ->
        val parts = (team as BoardText.Joined).parts.map { it.text() }
        flags += RowFlag(parts.joinToString(", ")) {
            OrderFlagShell(FlagTone.Neutral, Icons.Outlined.Group) { style, ink -> DotFlow(items = parts.map { AnnotatedString(it) }, style = style, color = ink, dotColor = ink) }
        }
    }
    dirtinessChipLabelRes(row.dirtinessLevel)?.let { res ->
        val text = stringResource(res)
        flags += RowFlag(text) { OrderFlag(text, FlagTone.Neutral, Icons.Outlined.AutoAwesome) }
    }
    return flags
}

/**
 * The reserved card: the app's selected stroke on the card; the hold in the head with *Decline* as a text link (a
 * full-width outlined button under the Confirm bar at large text), then the job's own row naming its day.
 */
@Composable
fun ReservedCard(
    row: OrderListItem,
    offer: PendingOffer,
    now: Instant,
    clash: OrderListItem?,
    declineBusy: Boolean,
    onOpen: () -> Unit,
    onConfirm: () -> Unit,
    onDecline: () -> Unit,
    modifier: Modifier = Modifier,
    tileWidth: Dp? = null,
) {
    val locale = appLocale()
    val zone = ZoneId.systemDefault()
    val rules = OrdersBoardRules
    val until = reservedUntilLabel(offer.respondByUtc, now.toEpochMilli())
    val then = OrdersBoardRules.tie(stringResource(R.string.available_reserved_then))
    val title = OrdersBoardRules.tie(stringResource(R.string.offers_card_title))
    val window = rules.window(rules.start(row), rules.minutes(row), zone, locale)
    val dayWord = rules.start(row)?.let { rules.dayName(it.atZone(zone).toLocalDate(), now.atZone(zone).toLocalDate(), locale).text() }
    val declineLabel = actionLabel(stringResource(R.string.offer_decline), dayWord?.lowercase(locale), window)
    val large = LocalDensity.current.fontScale >= 1.3f
    val scheme = MaterialTheme.colorScheme
    Column(
        modifier = modifier
            .fillMaxWidth()
            .clip(MaterialTheme.shapes.medium)
            .background(scheme.surface)
            .border(BorderStroke(1.5.dp, scheme.primary), MaterialTheme.shapes.medium),
    ) {
        Row(
            modifier = Modifier.padding(start = Spacing.M, end = Spacing.M, top = Spacing.M),
            horizontalArrangement = Arrangement.spacedBy(Spacing.S),
        ) {
            OrdersDateTile(start = rules.start(row), size = TileSize.Header, now = now)
            Column(Modifier.weight(1f)) {
                LinkBeside(
                    gap = Spacing.M,
                    start = {
                        Text(text = title, style = MaterialTheme.typography.titleMedium.copy(lineBreak = LineBreak.Heading), color = scheme.onSurface, modifier = Modifier.clearAndSetSemantics {})
                    },
                    link = if (large) {
                        null
                    } else {
                        {
                            if (declineBusy) {
                                CircularProgressIndicator(modifier = Modifier.padding(Spacing.XXS).size(18.dp), color = primaryText(), strokeWidth = 2.dp)
                            } else {
                                CleansiaTextLink(
                                    text = stringResource(R.string.offer_decline),
                                    onClick = onDecline,
                                    modifier = Modifier.semantics { contentDescription = declineLabel },
                                )
                            }
                        }
                    },
                )
                DotFlow(
                    items = listOf(styled(until, SpanStyle(color = primaryText())), styled(then, SpanStyle(color = scheme.onSurfaceVariant))),
                    style = MaterialTheme.typography.bodyMedium,
                    color = scheme.onSurface,
                    modifier = Modifier.padding(top = 2.dp).clearAndSetSemantics {},
                )
            }
        }
        ReservedJobRow(
            row = row,
            now = now,
            clash = clash,
            onOpen = onOpen,
            onConfirm = onConfirm,
            extraSentence = listOf(title, "$until, $then"),
            tileWidth = tileWidth,
            bottomPadding = if (large) 0.dp else Spacing.M,
        )
        if (large) {
            CleansiaOutlinedButton(
                text = stringResource(R.string.offer_decline),
                onClick = onDecline,
                size = CleansiaButtonSize.Medium,
                enabled = !declineBusy,
                contentColor = primaryText(),
                borderColor = scheme.primary,
                modifier = Modifier
                    .padding(start = Spacing.M, end = Spacing.M, top = Spacing.XS, bottom = Spacing.M)
                    .semantics { contentDescription = declineLabel },
            )
        }
    }
}

/**
 * Words with a text link at their end: the link's words on the first line's baseline and at least [gap] from them, the
 * words wrapping beside it. Under [startMin] of room the link drops under the words, at the end. The link's touch room
 * reaches past the line it sits on.
 */
@Composable
internal fun LinkBeside(
    gap: Dp,
    modifier: Modifier = Modifier,
    startMin: Dp = 0.dp,
    start: @Composable () -> Unit,
    link: (@Composable () -> Unit)?,
) {
    val linkLine = MaterialTheme.typography.labelLarge.lineHeight
    Layout(contents = listOf(start, { if (link != null) link() }), modifier = modifier) { (startM, linkM), constraints ->
        val width = constraints.maxWidth
        // At its own width: a link with an icon gives its words the room its row has left, so unbounded it would cut them.
        val linkP = linkM.firstOrNull()?.let { it.measure(Constraints.fixedWidth(it.maxIntrinsicWidth(Constraints.Infinity).coerceAtMost(width))) }
        if (linkP == null) {
            val startP = startM.first().measure(Constraints(maxWidth = width))
            return@Layout layout(width, startP.height) { startP.place(0, 0) }
        }
        val inset = LinkInset.roundToPx()
        val room = width - (linkP.width - inset + gap.roundToPx())
        val linkBase = linkP[FirstBaseline].takeIf { it != AlignmentLine.Unspecified } ?: (linkP.height / 2)
        if (room >= startMin.roundToPx()) {
            val startP = startM.first().measure(Constraints(maxWidth = room.coerceAtLeast(0)))
            val startBase = startP[FirstBaseline].takeIf { it != AlignmentLine.Unspecified } ?: 0
            layout(width, startP.height) {
                startP.place(0, 0)
                linkP.place(width - linkP.width + inset, startBase - linkBase)
            }
        } else {
            val startP = startM.first().measure(Constraints(maxWidth = width))
            val pad = ((linkP.height - linkLine.roundToPx()) / 2).coerceAtLeast(0)
            layout(width, startP.height + linkP.height - pad * 2) {
                startP.place(0, 0)
                linkP.place(width - linkP.width + inset, startP.height - pad)
            }
        }
    }
}

/** The text link's own padding, so its words end on the card's text edge. */
private val LinkInset = 4.dp

/** "Tomorrow, 1 November" / "Saturday, 7 November" for the spoken sentence, in the app locale. */
@Composable
internal fun dayAndDate(start: Instant, now: Instant): String {
    val locale = appLocale()
    val zone = ZoneId.systemDefault()
    val date = DateTimeFormatter.ofPattern("d MMMM", locale).format(start.atZone(zone))
    val relative = OrdersBoardRules.relativeDayRes(start, now, zone)?.let { stringResource(it) }
        ?: DateTimeFormatter.ofPattern("EEEE", locale).format(start.atZone(zone)).replaceFirstChar { it.titlecase(locale) }
    return "$relative, $date"
}

