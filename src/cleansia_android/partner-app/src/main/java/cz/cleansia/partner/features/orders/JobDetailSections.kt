package cz.cleansia.partner.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.FirstBaseline
import androidx.compose.ui.layout.layout
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.CleansiaButtonSize
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.OrderSectionHeader
import cz.cleansia.core.ui.theme.capBandAligned
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.partner.R
import cz.cleansia.partner.ui.theme.pendingInk
import java.time.Instant
import java.time.ZoneId
import java.time.temporal.ChronoUnit

/** Who the Customer section names: nobody (a run-sheet step holds her row), her contact once done, or that it was removed. */
internal enum class CustomerWho { None, Contact, Removed }

/**
 * What the reference sections of her own job hold, each fact once. [customerWords]: the customer's notes and special
 * instructions; [access]: how to get in, once Getting in is behind her; [scopeRows]: the scope as labelled rows, left
 * out while the checklist says it; [cashRule] and [payouts]: the Payment section's two lines.
 */
internal data class OwnSections(
    val who: CustomerWho,
    val customerWords: Boolean,
    val access: Boolean,
    val scopeRows: Boolean,
    val cashRule: Boolean,
    val payouts: Boolean,
)

/**
 * Before cleaning the customer's words are a section and her name and phone are the Setting off step's; while the
 * checklist is open (Cleaning, or a solo card job's Finishing) it is the scope; the crew gates and the cash step keep the
 * customer's words out, as they always did; once done the customer is the section's, and a past job's are removed.
 */
internal fun ownSections(status: Int?, step: RunStep, crew: Boolean, mateMarked: Boolean, cash: Boolean, past: Boolean, accessShown: Boolean): OwnSections {
    val checklistOpen = status == 4 && ((step == RunStep.Clean && !(crew && mateMarked)) || (step == RunStep.Finish && !crew && !cash))
    return OwnSections(
        who = when {
            status != 5 -> CustomerWho.None
            past -> CustomerWho.Removed
            else -> CustomerWho.Contact
        },
        customerWords = status == 2 || status == 3 || checklistOpen,
        access = status == 4 && accessShown,
        scopeRows = !checklistOpen,
        cashRule = cash,
        payouts = status == 5,
    )
}

/** How far off a job she has not taken is: "Today", "Tomorrow", "In 2 days". Its weekday is the calendar line's. */
internal fun notTakenDay(start: Instant, now: Instant, zone: ZoneId): BoardText {
    val days = ChronoUnit.DAYS.between(now.atZone(zone).toLocalDate(), start.atZone(zone).toLocalDate()).toInt()
    return when {
        days <= 0 -> BoardText.Res(R.string.day_today)
        days == 1 -> BoardText.Res(R.string.day_tomorrow)
        else -> BoardText.Plural(R.plurals.detail_in_days, days)
    }
}

/** A section of the sheet: 24 above it, the Core header with its icon (and a count), then its rows. */
@Composable
internal fun DetailSection(
    title: String,
    icon: ImageVector,
    modifier: Modifier = Modifier,
    subtitle: String? = null,
    content: @Composable ColumnScope.() -> Unit,
) {
    Column(modifier.fillMaxWidth().padding(top = 24.dp)) {
        Column(Modifier.semantics(mergeDescendants = true) { heading() }) {
            OrderSectionHeader(title = title, icon = icon, subtitle = subtitle)
        }
        content()
    }
}

/** A part's eyebrow inside a step or a section: "Cleaning checklist", "Photos", "How to get in". */
@Composable
internal fun PartHeading(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
        color = primaryText(),
        modifier = modifier.semantics { heading() },
    )
}

/** A label over the customer's own words, or over a column: "Notes", "Before · 2". */
@Composable
internal fun WordsLabel(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = modifier,
    )
}

/** The app's tinted flag: the tone's ink on its wash, the icon on the first line's cap band. Amber is money only. */
@Composable
internal fun OrderFlag(text: String, tone: FlagTone, icon: ImageVector, modifier: Modifier = Modifier) {
    OrderFlagShell(tone, icon, modifier) { style, ink -> Text(text = OrdersBoardRules.tie(text), style = style, color = ink) }
}

/** The flag's pill and icon around words of its own: a crew line in a separator flow, or a fact over what it means. */
@Composable
internal fun OrderFlagShell(
    tone: FlagTone,
    icon: ImageVector,
    modifier: Modifier = Modifier,
    words: @Composable (style: TextStyle, ink: Color) -> Unit,
) {
    val ink = flagInk(tone)
    val style = MaterialTheme.typography.bodyMedium
    Row(
        modifier
            .clip(RoundedCornerShape(12.dp))
            .background(flagFill(tone))
            .padding(horizontal = 10.dp, vertical = 4.dp),
    ) {
        Icon(imageVector = icon, contentDescription = null, tint = ink, modifier = capBandAligned(style).size(glyphSize(style)))
        Box(Modifier.padding(start = 6.dp).alignBy(FirstBaseline)) { words(style, ink) }
    }
}

@Composable
private fun flagFill(tone: FlagTone): Color = when (tone) {
    FlagTone.Neutral -> MaterialTheme.colorScheme.surfaceVariant
    FlagTone.Warn -> MaterialTheme.colorScheme.pendingInk.copy(alpha = 0.12f)
    FlagTone.Negative -> MaterialTheme.colorScheme.error.copy(alpha = 0.12f)
}

/** Flags in a flow, 6 apart. */
@OptIn(ExperimentalLayoutApi::class)
@Composable
internal fun FlagFlow(modifier: Modifier = Modifier, content: @Composable () -> Unit) {
    FlowRow(modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(6.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) { content() }
}

/** The failed read in view: what failed, what comes with it, and Retry. */
@Composable
internal fun DetailErrorCard(title: String, body: String?, onRetry: (() -> Unit)?, modifier: Modifier = Modifier) {
    val shape = RoundedCornerShape(16.dp)
    Column(
        modifier
            .fillMaxWidth()
            .clip(shape)
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, shape)
            .padding(16.dp),
    ) {
        ErrorWords(title, body)
        onRetry?.let { RetryButton(it, Modifier.padding(top = 12.dp)) }
    }
}

/** The failure's title and its line, the title announced as it appears. */
@Composable
internal fun ErrorWords(title: String, body: String?) {
    Text(
        text = OrdersBoardRules.tie(title),
        style = MaterialTheme.typography.titleMedium,
        color = MaterialTheme.colorScheme.onSurface,
        modifier = Modifier.semantics { heading() }.announcePolitely(),
    )
    body?.let {
        Text(
            text = OrdersBoardRules.tie(it),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 4.dp),
        )
    }
}

@Composable
internal fun RetryButton(onRetry: () -> Unit, modifier: Modifier = Modifier) {
    CleansiaOutlinedButton(
        text = stringResource(R.string.retry),
        onClick = onRetry,
        modifier = modifier,
        size = CleansiaButtonSize.Medium,
        leadingIcon = Icons.Outlined.Refresh,
        contentColor = primaryText(),
        borderColor = MaterialTheme.colorScheme.primary,
    )
}

/** Rows of a list on the sheet, split by rules. A row drawn for a carded list keeps its own side padding on the sheet's edges. */
@Composable
internal fun <T> FlatRows(items: List<T>, row: @Composable (T, Modifier) -> Unit) {
    items.forEachIndexed { index, item ->
        if (index > 0) HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        row(item, Modifier.bleed(16.dp))
    }
}

private fun Modifier.bleed(by: Dp): Modifier = layout { measurable, constraints ->
    val px = by.roundToPx()
    val placeable = measurable.measure(constraints.copy(minWidth = constraints.maxWidth + 2 * px, maxWidth = constraints.maxWidth + 2 * px))
    layout(constraints.maxWidth, placeable.height) { placeable.place(-px, 0) }
}
