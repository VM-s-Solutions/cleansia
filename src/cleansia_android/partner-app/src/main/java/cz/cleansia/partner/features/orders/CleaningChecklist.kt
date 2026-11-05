package cz.cleansia.partner.features.orders

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.selection.toggleable
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CheckboxDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.FirstBaseline
import androidx.compose.ui.layout.layout
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.capBandAligned
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderItem

/**
 * The cleaner's tick-list: the package with its rooms and baths first, then one row per extra by its catalogue name,
 * each box on its label's first line. Ticks live on this phone only, and say so; they gate nothing. Interactive once
 * cleaning has started.
 */
@OptIn(ExperimentalLayoutApi::class)
@Composable
fun CleaningChecklist(
    order: OrderItem,
    checkedIds: Set<String>,
    onToggle: (itemId: String, checked: Boolean) -> Unit,
    interactive: Boolean,
    modifier: Modifier = Modifier,
) {
    val locale = appLocale()
    val row = order.asBoardRow()
    val lead = OrdersBoardRules.scopeItems(row.copy(extras = null), locale.language).map { it.text() }
    val leadId = order.selectedPackages?.firstNotNullOfOrNull { it.id } ?: order.selectedServices?.firstNotNullOfOrNull { it.id } ?: "scope"
    val items = listOfNotNull(lead.takeIf { it.isNotEmpty() }?.let { leadId to it }) +
        OrdersBoardRules.extraSlugsOf(row).map { slug -> "extra:$slug" to listOf(nameForExtraSlug(slug)) }
    if (items.isEmpty()) return
    val done = items.count { it.first in checkedIds }
    val labelStyle = MaterialTheme.typography.bodyMedium

    Column(modifier.fillMaxWidth()) {
        FlowRow(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, itemVerticalAlignment = Alignment.CenterVertically) {
            PartHeading(stringResource(R.string.checklist_section_title), Modifier.padding(end = 8.dp))
            Text(
                text = stringResource(R.string.checklist_progress, done, items.size),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Column(Modifier.padding(top = 8.dp)) {
            items.forEach { (id, words) ->
                val checked = id in checkedIds
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(min = 48.dp)
                        .toggleable(value = checked, enabled = interactive, role = Role.Checkbox) { onToggle(id, it) },
                    verticalAlignment = Alignment.Top,
                ) {
                    Box(
                        Modifier
                            .then(capBandAligned(labelStyle))
                            .layout { measurable, constraints ->
                                val placeable = measurable.measure(constraints)
                                val inset = BoxInset.roundToPx()
                                layout(placeable.width - inset, placeable.height) { placeable.place(-inset, 0) }
                            }
                            .size(48.dp),
                        contentAlignment = Alignment.Center,
                    ) {
                        Checkbox(
                            checked = checked,
                            onCheckedChange = null,
                            enabled = interactive,
                            colors = CheckboxDefaults.colors(
                                checkedColor = MaterialTheme.colorScheme.primary,
                                uncheckedColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                checkmarkColor = MaterialTheme.colorScheme.onPrimary,
                            ),
                        )
                    }
                    DotFlow(
                        items = words.map { AnnotatedString(OrdersBoardRules.tie(it)) },
                        style = labelStyle,
                        color = MaterialTheme.colorScheme.onSurface,
                        modifier = Modifier.weight(1f).alignBy(FirstBaseline).padding(start = 4.dp, bottom = 12.dp),
                        tieLast = true,
                    )
                }
            }
        }
        Text(
            text = OrdersBoardRules.tie(stringResource(R.string.checklist_phone_only)),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 4.dp),
        )
    }
}

/** The 48 target's side margin to the 18 box it holds, so the box's edge stands on the column's edge. */
private val BoxInset = 15.dp
