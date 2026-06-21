package cz.cleansia.customer.features.recurring

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.AutoAwesome
import androidx.compose.material.icons.outlined.CalendarMonth
import androidx.compose.material.icons.outlined.DeleteOutline
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material.icons.outlined.Pause
import androidx.compose.material.icons.outlined.PlayArrow
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.customer.R
import cz.cleansia.customer.core.recurring.RecurrenceFrequency
import cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto
import cz.cleansia.core.ui.components.CleansiaDialog
import java.time.format.TextStyle
import java.util.Locale

/**
 * List of the user's recurring booking templates with pause/resume + delete
 * actions. Plus-only — entry point on the Profile tab is hidden for
 * non-Plus users; the screen itself doesn't double-gate. Templates here
 * spawn concrete Order rows via the backend's daily materializer.
 *
 * Create + edit ship via [CreateRecurringScreen] — entry points are the
 * empty-state CTA, the FAB on the populated list, and "Make this recurring"
 * on a Completed order's detail screen.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RecurringBookingsScreen(
    onBack: () -> Unit,
    onCreateNew: () -> Unit = {},
    viewModel: RecurringBookingsViewModel = hiltViewModel(),
) {
    val templates by viewModel.templates.collectAsStateWithLifecycle()
    val loading by viewModel.loading.collectAsStateWithLifecycle()
    val loaded by viewModel.loaded.collectAsStateWithLifecycle()
    val mutating by viewModel.mutating.collectAsStateWithLifecycle()

    var pendingDeleteId by remember { mutableStateOf<String?>(null) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(stringResource(R.string.recurring_bookings_title)) },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Outlined.ArrowBack, contentDescription = null)
                    }
                },
            )
        },
        // FAB only when the list is non-empty — empty state has its own
        // primary CTA (avoid stacking two "Create" affordances on the empty
        // screen, which is competing with the empty-state copy).
        floatingActionButton = {
            if (templates.isNotEmpty()) {
                androidx.compose.material3.ExtendedFloatingActionButton(
                    onClick = onCreateNew,
                    icon = { Icon(Icons.Outlined.Add, contentDescription = null) },
                    text = { Text(stringResource(R.string.recurring_bookings_create_fab)) },
                )
            }
        },
    ) { padding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding),
        ) {
            when {
                loading && !loaded -> {
                    CircularProgressIndicator(
                        modifier = Modifier
                            .align(Alignment.Center)
                            .size(40.dp),
                    )
                }
                templates.isEmpty() -> EmptyState(
                    modifier = Modifier.align(Alignment.Center),
                    onCreateNew = onCreateNew,
                )
                else -> LazyColumn(
                    contentPadding = androidx.compose.foundation.layout.PaddingValues(
                        start = 16.dp, end = 16.dp, top = 16.dp, bottom = 96.dp,
                    ),
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                    modifier = Modifier.fillMaxSize(),
                ) {
                    items(templates, key = { it.id }) { template ->
                        TemplateCard(
                            template = template,
                            isMutating = mutating == template.id,
                            onToggleActive = { viewModel.toggleActive(template.id, template.isActive) },
                            onDelete = { pendingDeleteId = template.id },
                        )
                    }
                }
            }
        }
    }

    pendingDeleteId?.let { id ->
        // Find the template so the dialog can show concrete schedule details
        // — abstract "this schedule?" copy was confusing per user feedback.
        val template = templates.firstOrNull { it.id == id }
        DeleteScheduleDialog(
            template = template,
            onConfirm = {
                viewModel.delete(id)
                pendingDeleteId = null
            },
            onDismiss = { pendingDeleteId = null },
        )
    }
}

@Composable
private fun EmptyState(modifier: Modifier = Modifier, onCreateNew: () -> Unit) {
    Column(
        modifier = modifier.padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Icon(
            Icons.Outlined.CalendarMonth,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(56.dp),
        )
        Spacer(Modifier.height(12.dp))
        Text(
            text = stringResource(R.string.recurring_bookings_empty_title),
            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
        )
        Spacer(Modifier.height(4.dp))
        Text(
            text = stringResource(R.string.recurring_bookings_empty_subtitle),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = androidx.compose.ui.text.style.TextAlign.Center,
        )
        Spacer(Modifier.height(20.dp))
        // Path A entry — blank-slate create form. Path B (pre-fill from a
        // past order) is reachable from the order-detail footer instead.
        androidx.compose.material3.Button(onClick = onCreateNew) {
            Text(stringResource(R.string.recurring_bookings_empty_cta))
        }
    }
}

/**
 * Recurring schedule card — restructured per user feedback. Layout:
 *
 *   ┌ tinted header strip (frequency icon + cadence label, paused badge) ┐
 *   │                                                                    │
 *   │  Tuesday at 10:00                                                  │
 *   │  📍 Address line                                                   │
 *   │  ─────────────────────────────                                     │
 *   │  ▶ Resume / ⏸ Pause                              🗑 Delete         │
 *   └────────────────────────────────────────────────────────────────────┘
 *
 * The tinted header gives the card a clear "this is a schedule" identity
 * vs. the plain bordered row from v1.
 */
@Composable
private fun TemplateCard(
    template: RecurringBookingTemplateDto,
    isMutating: Boolean,
    onToggleActive: () -> Unit,
    onDelete: () -> Unit,
) {
    val freq = RecurrenceFrequency.fromCode(template.frequency)
    val cadenceLabel = stringResource(
        when (freq) {
            RecurrenceFrequency.Weekly -> R.string.recurring_bookings_cadence_weekly
            RecurrenceFrequency.Biweekly -> R.string.recurring_bookings_cadence_biweekly
            RecurrenceFrequency.Monthly -> R.string.recurring_bookings_cadence_monthly
        },
    )
    val javaDow = if (template.dayOfWeek == 0) 7 else template.dayOfWeek
    val dayName = java.time.DayOfWeek.of(javaDow).getDisplayName(TextStyle.FULL, Locale.getDefault())
    val dayAtTime = stringResource(
        R.string.recurring_bookings_day_at_time, dayName, template.timeOfDay,
    )

    val cardShape = RoundedCornerShape(16.dp)
    val accent = MaterialTheme.colorScheme.primary
    val accentTint = accent.copy(alpha = if (template.isActive) 0.10f else 0.05f)

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(cardShape)
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, cardShape),
    ) {
        // ── Header strip ──
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .background(accentTint)
                .padding(horizontal = 16.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                imageVector = Icons.Outlined.AutoAwesome,
                contentDescription = null,
                tint = if (template.isActive) accent else MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(16.dp),
            )
            Spacer(Modifier.width(8.dp))
            Text(
                text = cadenceLabel,
                style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                color = if (template.isActive) accent else MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.weight(1f),
            )
            if (!template.isActive) {
                Text(
                    text = stringResource(R.string.recurring_bookings_paused_badge),
                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier
                        .clip(RoundedCornerShape(8.dp))
                        .background(MaterialTheme.colorScheme.surfaceVariant)
                        .padding(horizontal = 8.dp, vertical = 3.dp),
                )
            }
        }

        // ── Body ──
        Column(modifier = Modifier.padding(horizontal = 16.dp, vertical = 14.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    Icons.Outlined.Schedule,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(18.dp),
                )
                Spacer(Modifier.width(10.dp))
                Text(
                    text = dayAtTime,
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
            if (!template.addressLine.isNullOrBlank()) {
                Spacer(Modifier.height(8.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        Icons.Outlined.LocationOn,
                        contentDescription = null,
                        modifier = Modifier.size(18.dp),
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Spacer(Modifier.width(10.dp))
                    Text(
                        text = template.addressLine,
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }

        HorizontalDivider(
            color = MaterialTheme.colorScheme.outlineVariant,
            thickness = 1.dp,
        )

        // ── Action row ──
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 8.dp, vertical = 4.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            CardAction(
                icon = if (template.isActive) Icons.Outlined.Pause else Icons.Outlined.PlayArrow,
                label = stringResource(
                    if (template.isActive) R.string.recurring_bookings_pause
                    else R.string.recurring_bookings_resume,
                ),
                tint = MaterialTheme.colorScheme.onSurface,
                enabled = !isMutating,
                onClick = onToggleActive,
            )
            Spacer(Modifier.weight(1f))
            CardAction(
                icon = Icons.Outlined.DeleteOutline,
                label = stringResource(R.string.recurring_bookings_delete),
                tint = MaterialTheme.colorScheme.error,
                enabled = !isMutating,
                onClick = onDelete,
            )
        }
    }
}

@Composable
private fun CardAction(
    icon: ImageVector,
    label: String,
    tint: androidx.compose.ui.graphics.Color,
    enabled: Boolean,
    onClick: () -> Unit,
) {
    TextButton(onClick = onClick, enabled = enabled) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = tint,
            modifier = Modifier.size(18.dp),
        )
        Spacer(Modifier.width(6.dp))
        Text(text = label, color = tint)
    }
}

/**
 * Delete-confirm dialog — rewritten per user feedback. The previous copy
 * ("Future cleanings already scheduled won't be cancelled") confused users
 * because it sounded like deleting could cancel them. The new version names
 * the schedule that's about to go away, separates "what stops" from "what
 * stays," and recommends Pause as the lighter alternative.
 */
@Composable
private fun DeleteScheduleDialog(
    template: RecurringBookingTemplateDto?,
    onConfirm: () -> Unit,
    onDismiss: () -> Unit,
) {
    val scheduleSummary = template?.let {
        val freq = RecurrenceFrequency.fromCode(it.frequency)
        val cadence = stringResource(
            when (freq) {
                RecurrenceFrequency.Weekly -> R.string.recurring_bookings_cadence_weekly
                RecurrenceFrequency.Biweekly -> R.string.recurring_bookings_cadence_biweekly
                RecurrenceFrequency.Monthly -> R.string.recurring_bookings_cadence_monthly
            },
        )
        val javaDow = if (it.dayOfWeek == 0) 7 else it.dayOfWeek
        val day = java.time.DayOfWeek.of(javaDow).getDisplayName(TextStyle.FULL, Locale.getDefault())
        "$cadence · $day · ${it.timeOfDay}"
    }

    CleansiaDialog(
        onDismiss = onDismiss,
        title = stringResource(R.string.recurring_bookings_delete_dialog_title),
        destructive = true,
        confirmLabel = stringResource(R.string.recurring_bookings_delete_dialog_confirm),
        onConfirm = onConfirm,
        dismissLabel = stringResource(R.string.common_back),
        content = {
            Column {
                if (!scheduleSummary.isNullOrBlank()) {
                    Text(
                        text = scheduleSummary,
                        style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    Spacer(Modifier.height(8.dp))
                }
                Text(
                    text = stringResource(R.string.recurring_bookings_delete_dialog_what_stops),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
                Spacer(Modifier.height(6.dp))
                Text(
                    text = stringResource(R.string.recurring_bookings_delete_dialog_what_stays),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Spacer(Modifier.height(10.dp))
                Text(
                    text = stringResource(R.string.recurring_bookings_delete_dialog_pause_hint),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        },
    )
}
