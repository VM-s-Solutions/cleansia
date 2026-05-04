package cz.cleansia.customer.features.booking

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.catalog.PackageListItem
import cz.cleansia.customer.core.catalog.ServiceListItem
import cz.cleansia.customer.ui.components.CleansiaPrimaryButton

/** Opens the service details sheet. Service tap-to-select happens on the row body;
 * this sheet is pure information — no toggle affordance inside. Close on scrim tap
 * or explicit "Got it" button.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ServiceDetailsSheet(
    service: ServiceListItem,
    onDismiss: () -> Unit,
) {
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    val palette = service.category.palette()
    val name = localizedName(service.translations, service.name)
    val description = localizedDescription(service.translations, service.description)
    val categoryName = localizedName(service.category.translations, service.category.name)

    ModalBottomSheet(
        onDismissRequest = onDismiss,
        sheetState = sheetState,
        containerColor = MaterialTheme.colorScheme.surface,
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 24.dp)
                .padding(bottom = 8.dp),
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier.size(56.dp).background(palette.bg(), RoundedCornerShape(16.dp)),
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(palette.icon, null, tint = palette.tint, modifier = Modifier.size(28.dp))
                }
                Spacer(Modifier.width(14.dp))
                Column(Modifier.fillMaxWidth()) {
                    Text(
                        name,
                        style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    Spacer(Modifier.height(2.dp))
                    CategoryPill(label = categoryName, tint = palette.tint)
                }
            }

            if (!description.isNullOrBlank()) {
                Spacer(Modifier.height(20.dp))
                Text(
                    description,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }

            Spacer(Modifier.height(20.dp))
            PriceBreakdownRow(
                label = stringResource(R.string.details_base_price),
                value = "${service.basePrice.toInt()} CZK",
            )
            if (service.perRoomPrice > 0) {
                Spacer(Modifier.height(6.dp))
                PriceBreakdownRow(
                    label = stringResource(R.string.details_per_room),
                    value = "${service.perRoomPrice.toInt()} CZK",
                )
            }

            Spacer(Modifier.height(24.dp))
            CleansiaPrimaryButton(
                text = stringResource(R.string.common_got_it),
                onClick = onDismiss,
            )
            Spacer(Modifier.navigationBarsPadding())
        }
    }
}

/** Package details — pure information + a primary "Add to booking" / "Remove" button.
 * Unlike services, package selection happens through this sheet, not directly on the card.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PackageDetailsSheet(
    pkg: PackageListItem,
    isSelected: Boolean,
    onToggle: () -> Unit,
    onDismiss: () -> Unit,
) {
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    val name = localizedName(pkg.translations, pkg.name)
    val description = localizedDescription(pkg.translations, pkg.description)
    val included = pkg.includedServices.orEmpty()

    ModalBottomSheet(
        onDismissRequest = onDismiss,
        sheetState = sheetState,
        containerColor = MaterialTheme.colorScheme.surface,
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 24.dp)
                .padding(bottom = 8.dp),
        ) {
            Text(
                name,
                style = MaterialTheme.typography.headlineSmall.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Spacer(Modifier.height(4.dp))
            Text(
                "${pkg.price.toInt()} CZK",
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
            )

            if (!description.isNullOrBlank()) {
                Spacer(Modifier.height(16.dp))
                Text(
                    description,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }

            if (included.isNotEmpty()) {
                Spacer(Modifier.height(20.dp))
                Text(
                    stringResource(R.string.booking_package_includes),
                    style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Spacer(Modifier.height(8.dp))
                included.forEach { svc ->
                    DetailIncludedRow(name = localizedName(svc.translations, svc.name))
                }
            }

            Spacer(Modifier.height(24.dp))
            CleansiaPrimaryButton(
                text = if (isSelected) {
                    stringResource(R.string.details_remove_from_booking)
                } else {
                    stringResource(R.string.details_add_to_booking)
                },
                onClick = {
                    onToggle()
                    onDismiss()
                },
            )
            Spacer(Modifier.navigationBarsPadding())
        }
    }
}

@Composable
private fun CategoryPill(label: String, tint: Color) {
    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(999.dp))
            .background(tint.copy(alpha = 0.12f))
            .padding(horizontal = 10.dp, vertical = 4.dp),
    ) {
        Text(
            label,
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = tint,
        )
    }
}

@Composable
private fun PriceBreakdownRow(label: String, value: String) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            value,
            style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

@Composable
private fun DetailIncludedRow(name: String) {
    Row(
        modifier = Modifier.padding(vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            Icons.Outlined.Check,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(18.dp),
        )
        Spacer(Modifier.width(10.dp))
        Text(
            name,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

/** Shared: vertical gradient background used on the package hero — keeps the marketing
 * feel in the sheet header without duplicating the gradient builder.
 */
@Suppress("unused")
private fun brushFor(colors: List<Color>): Brush = Brush.verticalGradient(colors)
