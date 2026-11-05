package cz.cleansia.partner.features.orders

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.AutoAwesome
import androidx.compose.material.icons.outlined.Group
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Lock
import androidx.compose.material.icons.outlined.Payments
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.OrderInfoRow
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderListItem

/**
 * *Property & scope*, the job's one home for its scope: the package or service, the rooms and baths and the extras as
 * labelled rows ([rows] false while the checklist says them), then dirt when more than normal, and on a job she has not
 * taken ([own] false) the cash to collect and the crew with its seats, on her own job the team. The late joiner learns
 * the pay stays the same. While the job loads ([order] null) the board's row says the same. Nothing when it has nothing.
 */
@Composable
fun ScopeCard(order: OrderItem?, row: OrderListItem, lateJoin: Boolean, own: Boolean = false, rows: Boolean = true) {
    val locale = appLocale()
    val rules = OrdersBoardRules
    val lead = rules.scopeLead(row, locale.language)
    val property = listOfNotNull(
        row.rooms?.takeIf { it > 0 }?.let { BoardText.Plural(R.plurals.scope_rooms, it).text() },
        row.bathrooms?.takeIf { it > 0 }?.let { BoardText.Plural(R.plurals.scope_baths, it).text() },
    ).joinToString(rules.SEP) { rules.tieCount(it) }.takeIf { it.isNotEmpty() }
    val extras = rules.extraSlugsOf(row).map { slug -> rules.extraLabel(slug).text().replaceFirstChar { it.titlecase(locale) } }
        .joinToString(", ").takeIf { it.isNotEmpty() }
    val dirt = dirtinessLevelLabelRes(order?.dirtinessLevel ?: row.dirtinessLevel)
    val cash = if (own) null else rules.cashFlag(row, locale)
    val crewSize = (if (order != null) order.orderCrew()?.crewSize else row.requiredEmployees)?.takeIf { it > 1 }
    val hasRows = rows && (lead != null || property != null || extras != null)
    if (!hasRows && dirt == null && cash == null && crewSize == null) return
    DetailSection(title = stringResource(R.string.scope_section_title), icon = Icons.Outlined.Home) {
        if (hasRows) {
            lead?.let { ScopeRow(stringResource(R.string.scope_services_label), it) }
            property?.let { ScopeRow(stringResource(R.string.property), it) }
            extras?.let { ScopeRow(stringResource(R.string.scope_extras_label), it) }
        }
        if (dirt != null || cash != null || crewSize != null) {
            FlagFlow(Modifier.padding(top = if (hasRows) 10.dp else 0.dp)) {
                dirt?.let { OrderFlag(stringResource(it), FlagTone.Neutral, Icons.Outlined.AutoAwesome) }
                cash?.let { OrderFlag(it.text(), FlagTone.Warn, Icons.Outlined.Payments) }
                crewSize?.let { size ->
                    val taken = (order?.assignedEmployeesCount ?: row.assignedEmployeesCount) ?: 0
                    val words = if (own) {
                        BoardText.Plural(R.plurals.active_hero_team, size).text()
                    } else {
                        listOf(
                            BoardText.Plural(R.plurals.crew_size, size).text(),
                            if (taken > 0) BoardText.Plural(R.plurals.orders_flag_crew_taken, taken).text() else stringResource(R.string.orders_flag_crew_nobody),
                        ).joinToString(rules.SEP)
                    }
                    OrderFlag(words, FlagTone.Neutral, Icons.Outlined.Group)
                }
            }
        }
        if (lateJoin && crewSize != null) {
            Text(
                text = stringResource(R.string.detail_same_pay_late),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 8.dp),
            )
        }
    }
}

@Composable
private fun ScopeRow(label: String, value: String) {
    Box(Modifier.padding(vertical = 3.dp)) { OrderInfoRow(label = label, value = value) }
}

/** *Shown after you take it*, behind its lock: what taking the job releases. */
@Composable
fun ShownAfterTake() {
    DetailSection(title = stringResource(R.string.detail_shown_after_take), icon = Icons.Outlined.Lock) {
        DotFlow(
            items = listOf(
                stringResource(R.string.detail_hidden_address),
                stringResource(R.string.detail_hidden_contact),
                stringResource(R.string.access_section_title),
                stringResource(R.string.detail_hidden_notes),
            ).map { AnnotatedString(it) },
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}
