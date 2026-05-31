package cz.cleansia.partner.features.orders.components

import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
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
import androidx.compose.material.icons.automirrored.outlined.Message
import androidx.compose.material.icons.outlined.Map
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.Phone
import androidx.compose.material.icons.outlined.Place
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderAddress

/**
 * Customer card. Shows name and address always; phone + SMS action
 * chips appear once the order is mine (server flags this via
 * [isAssignedToCurrentUser]). Navigate also shows on unassigned offers
 * because knowing where the address is helps the cleaner decide whether
 * to take the job — but contact info stays hidden until ownership is
 * established, matching the Wolt-style PII gating server-side.
 *
 * When the order carries [OrderAddress.latitude]/[OrderAddress.longitude]
 * the navigate intent uses precise coordinates; otherwise it falls back
 * to a free-text address query.
 */
@Composable
fun CustomerCard(
    customerName: String?,
    customerPhone: String?,
    address: OrderAddress?,
    isAssignedToCurrentUser: Boolean,
    modifier: Modifier = Modifier,
) {
    val context = LocalContext.current
    val addressLine = address.formatSingleLine()

    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface,
        tonalElevation = 1.dp,
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            if (!customerName.isNullOrBlank()) {
                CustomerRow(
                    icon = Icons.Outlined.Person,
                    text = customerName,
                    isPrimary = true,
                )
            }
            if (addressLine != null) {
                if (!customerName.isNullOrBlank()) Spacer(Modifier.height(6.dp))
                CustomerRow(
                    icon = Icons.Outlined.Place,
                    text = addressLine,
                    isPrimary = false,
                )
            }

            val phoneNumber = customerPhone?.takeIf { it.isNotBlank() }
            val showCall = isAssignedToCurrentUser && phoneNumber != null
            val showSms = isAssignedToCurrentUser && phoneNumber != null
            val showNavigate = addressLine != null

            if (showCall || showSms || showNavigate) {
                Spacer(Modifier.height(12.dp))
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                Spacer(Modifier.height(12.dp))
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    if (showCall) {
                        ActionChip(
                            icon = Icons.Outlined.Phone,
                            label = stringResource(R.string.action_call),
                            onClick = { launchCall(context, phoneNumber!!) },
                            modifier = Modifier.weight(1f),
                        )
                    }
                    if (showSms) {
                        ActionChip(
                            icon = Icons.AutoMirrored.Outlined.Message,
                            label = stringResource(R.string.action_sms),
                            onClick = { launchSms(context, phoneNumber!!) },
                            modifier = Modifier.weight(1f),
                        )
                    }
                    if (showNavigate) {
                        ActionChip(
                            icon = Icons.Outlined.Map,
                            label = stringResource(R.string.action_navigate),
                            onClick = {
                                launchNavigation(
                                    context = context,
                                    latitude = address?.latitude,
                                    longitude = address?.longitude,
                                    addressFallback = addressLine!!,
                                )
                            },
                            modifier = Modifier.weight(1f),
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun CustomerRow(
    icon: ImageVector,
    text: String,
    isPrimary: Boolean,
) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            modifier = Modifier.size(18.dp),
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.width(10.dp))
        Text(
            text = text,
            style = if (isPrimary)
                MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold)
            else
                MaterialTheme.typography.bodyMedium,
            color = if (isPrimary)
                MaterialTheme.colorScheme.onSurface
            else
                MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun ActionChip(
    icon: ImageVector,
    label: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Box(
        modifier = modifier
            .height(40.dp)
            .clip(RoundedCornerShape(20.dp))
            .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.10f))
            .clickable { onClick() }
            .padding(horizontal = 12.dp),
        contentAlignment = Alignment.Center,
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                modifier = Modifier.size(16.dp),
                tint = MaterialTheme.colorScheme.primary,
            )
            Spacer(Modifier.width(6.dp))
            Text(
                text = label,
                style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
            )
        }
    }
}

private fun launchCall(context: Context, phoneNumber: String) {
    // ACTION_DIAL (not ACTION_CALL) — opens the dialer pre-filled, doesn't
    // place the call automatically. No CALL_PHONE permission required and
    // gives the cleaner a chance to abort.
    val intent = Intent(Intent.ACTION_DIAL, Uri.parse("tel:${phoneNumber.replace(" ", "")}"))
        .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
    runCatching { context.startActivity(intent) }
}

private fun launchSms(context: Context, phoneNumber: String) {
    val intent = Intent(Intent.ACTION_SENDTO, Uri.parse("smsto:${phoneNumber.replace(" ", "")}"))
        .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
    runCatching { context.startActivity(intent) }
}

private fun launchNavigation(
    context: Context,
    latitude: Double?,
    longitude: Double?,
    addressFallback: String,
) {
    // Prefer precise lat/lng when the order carries them — keeps the
    // maps app from doing a fresh geocode round-trip (and from
    // misresolving in places where the customer's address has typos).
    // Falls back to a free-text query when coords are missing, which
    // happens for older orders that pre-date the geocoding backfill.
    val uri = if (latitude != null && longitude != null) {
        Uri.parse("geo:$latitude,$longitude?q=$latitude,$longitude(${Uri.encode(addressFallback)})")
    } else {
        Uri.parse("geo:0,0?q=${Uri.encode(addressFallback)}")
    }
    val intent = Intent(Intent.ACTION_VIEW, uri)
        .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
    runCatching { context.startActivity(intent) }
}
