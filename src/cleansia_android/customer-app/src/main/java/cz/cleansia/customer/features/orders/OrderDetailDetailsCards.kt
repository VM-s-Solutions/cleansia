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
import cz.cleansia.customer.R
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.customer.core.orders.AssignedEmployeeDto
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderPackageDetailsDto
import cz.cleansia.customer.core.orders.OrderServiceDetailsDto

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
            value = stringResource(
                R.string.order_detail_rooms_bathrooms,
                order.rooms,
                order.bathrooms,
            ),
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
private fun prettifyExtraKey(key: String): String {
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
                        text = svc.name ?: "—",
                        style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    svc.description?.takeIf { it.isNotBlank() }?.let { desc ->
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
                        text = pkg.name ?: "—",
                        style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    pkg.description?.takeIf { it.isNotBlank() }?.let { desc ->
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
    val blocks = listOfNotNull(
        order.specialInstructions?.takeIf { it.isNotBlank() }
            ?.let { stringResource(R.string.order_detail_special_instructions) to it },
        order.accessInstructions?.takeIf { it.isNotBlank() }
            ?.let { stringResource(R.string.order_detail_access_instructions) to it },
        order.notes?.takeIf { it.isNotBlank() }
            ?.let { stringResource(R.string.order_detail_notes) to it },
    )
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_instructions))
        Spacer(Modifier.height(6.dp))
        blocks.forEachIndexed { idx, (label, text) ->
            if (idx > 0) {
                Spacer(Modifier.height(8.dp))
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                Spacer(Modifier.height(8.dp))
            }
            Text(
                text = label,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = text,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
            )
        }
    }
}

/* ── Assigned cleaners ── */

@Composable
internal fun AssignedCleanersCard(employees: List<AssignedEmployeeDto>) {
    val context = LocalContext.current
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_cleaners))
        Spacer(Modifier.height(8.dp))
        employees.forEachIndexed { idx, emp ->
            if (idx > 0) Spacer(Modifier.height(10.dp))
            val displayName = emp.fullName?.takeIf { it.isNotBlank() }
                ?: stringResource(R.string.order_detail_cleaner_fallback)
            val initial = displayName.firstOrNull()?.uppercaseChar()?.toString().orEmpty()
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier
                        .size(40.dp)
                        .background(MaterialTheme.colorScheme.primaryContainer, CircleShape),
                    contentAlignment = Alignment.Center,
                ) {
                    Text(
                        text = initial,
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.primary,
                    )
                }
                Spacer(Modifier.width(12.dp))
                Column(Modifier.weight(1f)) {
                    Text(
                        text = displayName,
                        style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    emp.phoneNumber?.takeIf { it.isNotBlank() }?.let { phone ->
                        Text(
                            text = phone,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
                emp.phoneNumber?.takeIf { it.isNotBlank() }?.let { phone ->
                    Box(
                        modifier = Modifier
                            .size(36.dp)
                            .clip(CircleShape)
                            .background(MaterialTheme.colorScheme.primaryContainer)
                            .clickable {
                                val intent = Intent(Intent.ACTION_DIAL, Uri.parse("tel:$phone"))
                                // Wrap in try/catch — some devices (e.g. tablets without a dialer)
                                // can throw ActivityNotFoundException. We silently no-op on failure
                                // since this is a Wave 1 convenience tap; Wave 2 may surface a snackbar.
                                runCatching {
                                    ContextCompat.startActivity(context, intent, null)
                                }
                            },
                        contentAlignment = Alignment.Center,
                    ) {
                        Icon(
                            Icons.Outlined.Phone,
                            contentDescription = null,
                            tint = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.size(18.dp),
                        )
                    }
                }
            }
        }
    }
}
