package cz.cleansia.partner.features.orders

import androidx.compose.animation.animateContentSize
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.PlaylistAddCheck
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CheckboxDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderItem

/**
 * Single checklist entry — the view layer is agnostic to whether it
 * came from a service, package or extra. Parent component groups
 * entries by source.
 */
data class ChecklistItem(
    val id: String,
    val label: String,
    val leadingGlyph: String? = null,
)

/**
 * Cleaner's tick-list. Rendered while the order is in any
 * status >= Confirmed but only interactive once the cleaner has
 * actually started the job (status = InProgress) — that prevents
 * pre-checking items "on the bus" and accidentally enabling the
 * Complete button before the work is real.
 */
@Composable
fun CleaningChecklist(
    order: OrderItem,
    checkedIds: Set<String>,
    onToggle: (itemId: String, checked: Boolean) -> Unit,
    interactive: Boolean,
    modifier: Modifier = Modifier,
) {
    val services = order.selectedServices.orEmpty()
        .mapNotNull { s ->
            val id = s.id ?: return@mapNotNull null
            val name = s.name?.takeIf { it.isNotBlank() } ?: return@mapNotNull null
            ChecklistItem(id = id, label = name)
        }
    val packages = order.selectedPackages.orEmpty()
        .mapNotNull { p ->
            val id = p.id ?: return@mapNotNull null
            val name = p.name?.takeIf { it.isNotBlank() } ?: return@mapNotNull null
            ChecklistItem(id = id, label = name)
        }
    val extras = order.extras.orEmpty()
        .filterValues { it }
        .keys
        .map { slug ->
            ChecklistItem(
                id = "extra:$slug",
                label = nameForExtraSlug(slug),
                leadingGlyph = emojiForExtraSlug(slug),
            )
        }

    val total = services.size + packages.size + extras.size
    if (total == 0) return

    val doneCount = services.count { it.id in checkedIds } +
        packages.count { it.id in checkedIds } +
        extras.count { it.id in checkedIds }
    val allDone = doneCount == total

    OrderSectionCard(
        title = stringResource(R.string.checklist_section_title),
        icon = Icons.AutoMirrored.Outlined.PlaylistAddCheck,
        modifier = modifier,
    ) {
        Column(
            modifier = Modifier.animateContentSize(),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            ChecklistProgressRow(
                doneCount = doneCount,
                total = total,
                interactive = interactive,
                allDone = allDone,
            )

            if (services.isNotEmpty()) {
                ChecklistGroup(
                    label = stringResource(R.string.checklist_services_label),
                    items = services,
                    checkedIds = checkedIds,
                    interactive = interactive,
                    onToggle = onToggle,
                )
            }
            if (packages.isNotEmpty()) {
                ChecklistGroup(
                    label = stringResource(R.string.checklist_packages_label),
                    items = packages,
                    checkedIds = checkedIds,
                    interactive = interactive,
                    onToggle = onToggle,
                )
            }
            if (extras.isNotEmpty()) {
                ChecklistGroup(
                    label = stringResource(R.string.checklist_extras_label),
                    items = extras,
                    checkedIds = checkedIds,
                    interactive = interactive,
                    onToggle = onToggle,
                )
            }
        }
    }
}

@Composable
private fun ChecklistProgressRow(
    doneCount: Int,
    total: Int,
    interactive: Boolean,
    allDone: Boolean,
) {
    val progress = if (total == 0) 0f else doneCount.toFloat() / total
    Column {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Text(
                text = stringResource(R.string.checklist_progress, doneCount, total),
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = if (allDone) MaterialTheme.colorScheme.primary
                    else MaterialTheme.colorScheme.onSurface,
            )
            if (allDone) {
                Icon(
                    imageVector = Icons.Outlined.CheckCircle,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(24.dp),
                )
            }
        }
        Spacer(Modifier.height(8.dp))
        LinearProgressIndicator(
            progress = { progress.coerceIn(0f, 1f) },
            modifier = Modifier
                .fillMaxWidth()
                .height(6.dp)
                .clip(RoundedCornerShape(3.dp)),
            color = if (allDone) MaterialTheme.colorScheme.primary
                else MaterialTheme.colorScheme.primary.copy(alpha = 0.8f),
            trackColor = MaterialTheme.colorScheme.surfaceVariant,
            // Hide the M3 stop-indicator dot so the bar reads as a
            // clean fill rather than "progress + something else."
            drawStopIndicator = {},
            gapSize = 0.dp,
            strokeCap = androidx.compose.ui.graphics.StrokeCap.Round,
        )
        if (!interactive) {
            Spacer(Modifier.height(4.dp))
            Text(
                text = stringResource(R.string.checklist_locked_hint),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        } else if (allDone) {
            Spacer(Modifier.height(4.dp))
            Text(
                text = stringResource(R.string.checklist_all_done_hint),
                style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
            )
        }
    }
}

@Composable
private fun ChecklistGroup(
    label: String,
    items: List<ChecklistItem>,
    checkedIds: Set<String>,
    interactive: Boolean,
    onToggle: (itemId: String, checked: Boolean) -> Unit,
) {
    Column {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(bottom = 4.dp),
        )
        items.forEach { item ->
            ChecklistRow(
                item = item,
                checked = item.id in checkedIds,
                interactive = interactive,
                onToggle = { value -> onToggle(item.id, value) },
            )
        }
    }
}

@Composable
private fun ChecklistRow(
    item: ChecklistItem,
    checked: Boolean,
    interactive: Boolean,
    onToggle: (Boolean) -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 2.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Checkbox(
            checked = checked,
            onCheckedChange = if (interactive) onToggle else null,
            enabled = interactive,
            colors = CheckboxDefaults.colors(
                checkedColor = MaterialTheme.colorScheme.primary,
                uncheckedColor = MaterialTheme.colorScheme.outline,
                disabledCheckedColor = MaterialTheme.colorScheme.primary.copy(alpha = 0.5f),
                disabledUncheckedColor = MaterialTheme.colorScheme.outline.copy(alpha = 0.5f),
            ),
        )
        Spacer(Modifier.width(4.dp))
        if (item.leadingGlyph != null) {
            Text(
                text = item.leadingGlyph,
                style = MaterialTheme.typography.bodyLarge,
                modifier = Modifier.padding(end = 8.dp),
            )
        }
        Text(
            text = item.label,
            style = MaterialTheme.typography.bodyMedium.copy(
                fontWeight = if (checked) FontWeight.Normal else FontWeight.Medium,
                textDecoration = if (checked) TextDecoration.LineThrough else TextDecoration.None,
            ),
            color = if (checked) MaterialTheme.colorScheme.onSurfaceVariant
                else MaterialTheme.colorScheme.onSurface,
        )
    }
}

