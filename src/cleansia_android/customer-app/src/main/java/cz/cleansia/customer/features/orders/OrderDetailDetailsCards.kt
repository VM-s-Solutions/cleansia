package cz.cleansia.customer.features.orders

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.contentDescription
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import kotlinx.coroutines.delay
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Phone
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import cz.cleansia.core.ui.theme.primaryIconOnContainer
import cz.cleansia.core.ui.theme.primaryTextOnContainer
import cz.cleansia.customer.R
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.customer.core.orders.AssignedEmployeeDto
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderPackageDetailsDto
import cz.cleansia.customer.core.orders.OrderServiceDetailsDto
import cz.cleansia.customer.features.booking.localizedDescription
import cz.cleansia.customer.features.booking.localizedName
import cz.cleansia.customer.features.booking.titleRes
import cz.cleansia.customer.features.profile.AvatarPhoto
import cz.cleansia.customer.features.profile.ProfileAvatarContent

/* ── Cleaning details ── */

@OptIn(ExperimentalLayoutApi::class)
@Composable
internal fun CleaningDetailsCard(order: OrderDetailDto) {
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_section_details))
        Spacer(Modifier.height(8.dp))

        // Rooms + bathrooms (backend always returns 0 if unset — show anyway).
        InfoRow(
            label = stringResource(R.string.order_detail_rooms),
            // Two counts, so this cannot be one <plurals> — Android selects on a single quantity.
            // Composed from the two resources the booking wizard already uses, which also keeps the
            // wizard and the order detail reading identically.
            value = roomsAndBathrooms(order.rooms, order.bathrooms),
        )
        Spacer(Modifier.height(6.dp))
        InfoRow(
            label = stringResource(R.string.dirtiness_level_label),
            value = stringResource(order.dirtinessLevel.titleRes()),
        )
        Spacer(Modifier.height(6.dp))
        InfoRow(
            label = stringResource(R.string.order_detail_estimated),
            value = if (order.estimatedTime > 0) {
                stringResource(R.string.order_detail_duration_minutes, order.estimatedTime)
            } else {
                "—"
            },
        )

        val actualMinutes = order.actualCompletionTime ?: 0
        if (actualMinutes > 0) {
            Spacer(Modifier.height(6.dp))
            InfoRow(
                label = stringResource(R.string.order_detail_actual_time),
                value = stringResource(R.string.order_detail_duration_minutes, actualMinutes),
            )
        }

        // Authoritative completion timestamp — only present on terminal
        // Completed orders. Reads order.completedAt directly (backend's
        // dedicated DB column) instead of inferring from statusHistory.
        if (!order.completedAt.isNullOrBlank()) {
            Spacer(Modifier.height(6.dp))
            InfoRow(
                label = stringResource(R.string.order_detail_completed_at),
                value = cz.cleansia.core.format.formatOrderDateTime(order.completedAt),
            )
        }

        order.completionNotes?.takeIf { it.isNotBlank() }?.let { notes ->
            Spacer(Modifier.height(10.dp))
            Text(
                stringResource(R.string.order_detail_completion_notes),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = notes,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
            )
        }

        // Extras — only those flagged `true` in the map. Skip block entirely if none.
        val activeExtras = order.extras.orEmpty().filter { it.value }.keys.toList()
        if (activeExtras.isNotEmpty()) {
            Spacer(Modifier.height(10.dp))
            Text(
                stringResource(R.string.order_detail_extras),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(6.dp))
            FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                activeExtras.forEach { key ->
                    Text(
                        text = prettifyExtraKey(key),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier
                            .padding(vertical = 3.dp)
                            .background(
                                MaterialTheme.colorScheme.surfaceVariant,
                                RoundedCornerShape(999.dp),
                            )
                            .padding(horizontal = 10.dp, vertical = 4.dp),
                    )
                }
            }
        }
    }
}

/**
 * Turn an extras-map key like `eco_products` or `stainRemoval` into a
 * readable label ("Eco Products" / "Stain Removal"). Fallback only — backend
 * may later localise these and send a display name.
 */
internal fun prettifyExtraKey(key: String): String {
    if (key.isBlank()) return key
    // Split camelCase + snake/kebab into words, then title-case each.
    val spaced = key
        .replace('_', ' ')
        .replace('-', ' ')
        .replace(Regex("([a-z])([A-Z])"), "$1 $2")
    return spaced.split(' ').joinToString(" ") { word ->
        word.lowercase().replaceFirstChar { c -> c.titlecase() }
    }
}

/* ── Services ── */

@Composable
internal fun ServicesCard(services: List<OrderServiceDetailsDto>) {
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_services_header))
        Spacer(Modifier.height(6.dp))
        services.forEachIndexed { idx, svc ->
            if (idx > 0) {
                Spacer(Modifier.height(6.dp))
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                Spacer(Modifier.height(6.dp))
            }
            Row(verticalAlignment = Alignment.Top) {
                Column(Modifier.weight(1f)) {
                    Text(
                        text = svc.name?.let { localizedName(svc.translations, it) } ?: "—",
                        style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    localizedDescription(svc.translations, svc.description)?.takeIf { it.isNotBlank() }?.let { desc ->
                        Text(
                            text = desc,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            maxLines = 2,
                        )
                    }
                }
                if (svc.estimatedTime > 0) {
                    Spacer(Modifier.width(8.dp))
                    TimeChip(minutes = svc.estimatedTime)
                }
            }
        }
    }
}

@Composable
private fun TimeChip(minutes: Int) {
    Text(
        text = stringResource(R.string.order_detail_duration_minutes, minutes),
        style = MaterialTheme.typography.labelSmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier
            .background(
                MaterialTheme.colorScheme.surfaceVariant,
                RoundedCornerShape(999.dp),
            )
            .padding(horizontal = 10.dp, vertical = 4.dp),
    )
}

/* ── Packages ── */

@Composable
internal fun PackagesCard(packages: List<OrderPackageDetailsDto>) {
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_packages_header))
        Spacer(Modifier.height(6.dp))
        packages.forEachIndexed { idx, pkg ->
            if (idx > 0) {
                Spacer(Modifier.height(6.dp))
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                Spacer(Modifier.height(6.dp))
            }
            Row(verticalAlignment = Alignment.Top) {
                Column(Modifier.weight(1f)) {
                    Text(
                        text = pkg.name?.let { localizedName(pkg.translations, it) } ?: "—",
                        style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    localizedDescription(pkg.translations, pkg.description)?.takeIf { it.isNotBlank() }?.let { desc ->
                        Text(
                            text = desc,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            maxLines = 2,
                        )
                    }
                    pkg.includedServices?.takeIf { it.isNotEmpty() }?.let { included ->
                        Spacer(Modifier.height(2.dp))
                        Text(
                            text = included.joinToString(", "),
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
                Spacer(Modifier.width(8.dp))
                Text(
                    text = formatOrderPrice(pkg.price, pkg.currencyCode),
                    style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onBackground,
                )
            }
        }
    }
}

/* ── Instructions ── */

@Composable
internal fun InstructionsCard(order: OrderDetailDto) {
    var accessRevealed by remember(order.id) { mutableStateOf(false) }
    LifecycleEventEffect(Lifecycle.Event.ON_PAUSE) { accessRevealed = false }
    LaunchedEffect(accessRevealed) {
        if (accessRevealed) {
            delay(60_000L)
            accessRevealed = false
        }
    }
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_instructions))
        val blocks = listOfNotNull(
            order.specialInstructions?.takeIf { it.isNotBlank() }?.let { stringResource(R.string.order_detail_special_instructions) to it },
            order.notes?.takeIf { it.isNotBlank() }?.let { stringResource(R.string.order_detail_notes) to it },
        )
        blocks.forEach { (label, text) ->
            InstructionText(label, text)
        }
        order.accessInstructions?.takeIf { it.isNotBlank() }?.let { access ->
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Text(stringResource(R.string.order_detail_access_instructions),
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.weight(1f))
                cz.cleansia.core.ui.components.CleansiaTextButton(onClick = { accessRevealed = !accessRevealed }) {
                    Text(stringResource(if (accessRevealed) R.string.order_detail_hide_access else R.string.order_detail_show_access))
                }
            }
            if (accessRevealed) {
                Text(access, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface)
            }
        }
    }
}

@Composable
private fun InstructionText(label: String, value: String) {
    Column(Modifier.padding(vertical = 6.dp)) {
        Text(label, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Spacer(Modifier.height(2.dp))
        Text(value, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface)
    }
}

/* ── Assigned cleaners ── */

@Composable
internal fun AssignedCleanersCard(employees: List<AssignedEmployeeDto>) {
    val context = LocalContext.current
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_cleaners))
        LazyRow(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            itemsIndexed(employees) { _, emp ->
                val displayName = emp.fullName?.takeIf { it.isNotBlank() } ?: stringResource(R.string.order_detail_cleaner_fallback)
                val callLabel = stringResource(R.string.order_detail_call_cleaner, displayName)
                Row(if (employees.size > 1) Modifier.width(230.dp) else Modifier.fillParentMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                    Box(Modifier.size(40.dp).background(MaterialTheme.colorScheme.primaryContainer, CircleShape), contentAlignment = Alignment.Center) {
                        ProfileAvatarContent(
                            initials = cleanerInitials(emp.fullName),
                            initialsStyle = MaterialTheme.typography.titleMedium,
                            initialsColor = MaterialTheme.colorScheme.primaryTextOnContainer,
                            photo = emp.avatarUrl?.let { AvatarPhoto.Remote(it, emp.avatarFileName) },
                        )
                    }
                    Spacer(Modifier.width(10.dp))
                    Column(Modifier.weight(1f)) {
                        Text(displayName, style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold), maxLines = 1, overflow = TextOverflow.Ellipsis)
                        emp.phoneNumber?.takeIf { it.isNotBlank() }?.let { phone ->
                            Text(phone, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1)
                        }
                    }
                    emp.phoneNumber?.takeIf { it.isNotBlank() }?.let { phone ->
                        androidx.compose.material3.IconButton(onClick = {
                            runCatching { ContextCompat.startActivity(context, Intent(Intent.ACTION_DIAL, Uri.parse("tel:$phone")), null) }
                        }, modifier = Modifier.size(48.dp)) {
                            Icon(Icons.Outlined.Phone, contentDescription = callLabel, tint = MaterialTheme.colorScheme.primaryIconOnContainer, modifier = Modifier.size(20.dp))
                        }
                    }
                }
            }
        }
    }
}

internal fun cleanerInitials(visibleName: String?): String {
    val parts = visibleName.orEmpty().trim().split(Regex("\\s+")).filter { it.isNotBlank() }
    val ends = if (parts.size > 1) listOf(parts.first(), parts.last()) else parts
    return ends.mapNotNull { it.firstOrNull()?.takeIf(Char::isLetter)?.uppercaseChar() }
        .joinToString("").ifBlank { "?" }
}
