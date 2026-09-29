package cz.cleansia.partner.features.orders

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.DoorFront
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.res.stringResource
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.format.formatOrderTime
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.api.model.PhotoType
import java.time.Duration
import java.time.Instant
import kotlinx.coroutines.delay

/** `BookingPolicy.LockoutWaitMinutes`: how long past the booked start the cleaner waits before reporting. */
internal const val LOCKOUT_WAIT_MINUTES = 15L

internal sealed interface LockoutStanding {
    data object Hidden : LockoutStanding
    data class NotYet(val opensAt: Instant) : LockoutStanding
    data object Open : LockoutStanding
    data class Reported(val reportedAt: String?, val callAttempts: String?) : LockoutStanding
}

/**
 * Mirrors `ReportOrderLockout`: the crew of a Confirmed, on-the-way or in-progress job may report it
 * once, from [LOCKOUT_WAIT_MINUTES] past the booked start.
 */
internal fun OrderItem.lockoutStanding(now: Instant): LockoutStanding {
    val status = orderStatus.toOrderStatus()
    val working = status == OrderStatus._2 || status == OrderStatus._3 || status == OrderStatus._4
    if (isAssignedToCurrentUser != true || !working) return LockoutStanding.Hidden
    if (!lockoutReportedAt.isNullOrBlank()) {
        return LockoutStanding.Reported(lockoutReportedAt, lockoutCallAttempts)
    }
    val start = cleaningDateTime?.let { runCatching { Instant.parse(it) }.getOrNull() }
        ?: return LockoutStanding.Hidden
    val opensAt = start.plus(Duration.ofMinutes(LOCKOUT_WAIT_MINUTES))
    return if (now.isBefore(opensAt)) LockoutStanding.NotYet(opensAt) else LockoutStanding.Open
}

@Composable
internal fun LockoutCard(
    order: OrderItem,
    isReporting: Boolean,
    actionsEnabled: Boolean,
    onReport: (String) -> Unit,
) {
    var now by remember { mutableStateOf(Instant.now()) }
    val standing = order.lockoutStanding(now)
    if (standing is LockoutStanding.NotYet) {
        LaunchedEffect(standing.opensAt) {
            delay(Duration.between(Instant.now(), standing.opensAt).toMillis().coerceAtLeast(0L))
            now = Instant.now()
        }
    }

    when (standing) {
        LockoutStanding.Hidden -> Unit
        is LockoutStanding.NotYet -> OrderSectionCard(
            title = stringResource(R.string.lockout_card_title),
            icon = Icons.Outlined.DoorFront,
        ) {
            Text(
                text = stringResource(
                    R.string.lockout_card_not_yet,
                    formatOrderTime(standing.opensAt.toString()),
                    LOCKOUT_WAIT_MINUTES,
                ),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        LockoutStanding.Open -> OpenLockoutCard(
            isReporting = isReporting,
            actionsEnabled = actionsEnabled,
            onReport = onReport,
        )
        is LockoutStanding.Reported -> OrderSectionCard(
            title = stringResource(R.string.lockout_reported_title),
            icon = Icons.Outlined.DoorFront,
        ) {
            Column(verticalArrangement = Arrangement.spacedBy(Spacing.S)) {
                Text(
                    text = stringResource(R.string.lockout_reported_body, formatOrderTime(standing.reportedAt)),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
                standing.callAttempts?.takeIf { it.isNotBlank() }?.let { calls ->
                    Text(
                        text = stringResource(R.string.lockout_reported_calls, calls),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }
    }
}

@Composable
private fun OpenLockoutCard(
    isReporting: Boolean,
    actionsEnabled: Boolean,
    onReport: (String) -> Unit,
    photosViewModel: OrderPhotosViewModel = hiltViewModel(),
) {
    val photosState by photosViewModel.uiState.collectAsStateWithLifecycle()
    val mutation by photosViewModel.mutation.collectAsStateWithLifecycle()
    val entrancePhotos = (photosState as? OrderPhotosUiState.Loaded)?.photos.orEmpty()
        .filter { it.photoType == PhotoType._3 }
    var sheetOpen by remember { mutableStateOf(false) }
    var note by rememberSaveable { mutableStateOf("") }

    OrderSectionCard(
        title = stringResource(R.string.lockout_card_title),
        icon = Icons.Outlined.DoorFront,
    ) {
        Column(verticalArrangement = Arrangement.spacedBy(Spacing.M)) {
            Text(
                text = stringResource(R.string.lockout_card_body),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Column {
                PhotoRail(
                    title = stringResource(R.string.lockout_entrance_photo),
                    type = PhotoType._3,
                    photos = entrancePhotos,
                    isUploading = mutation.isUploading,
                    deletingId = mutation.deletingId,
                    onUpload = photosViewModel::compressAndUpload,
                    onDelete = photosViewModel::delete,
                    isReadOnly = false,
                )
            }
            if (entrancePhotos.isEmpty()) {
                Text(
                    text = stringResource(R.string.lockout_photo_needed),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            CleansiaOutlinedButton(
                text = stringResource(R.string.lockout_report_action),
                onClick = { sheetOpen = true },
                enabled = entrancePhotos.isNotEmpty() && actionsEnabled && !isReporting,
            )
        }
    }

    if (sheetOpen) {
        TextEntryBottomSheet(
            title = stringResource(R.string.lockout_sheet_title),
            description = stringResource(R.string.lockout_sheet_description),
            label = stringResource(R.string.lockout_call_attempts_label),
            initialText = note,
            isSaving = isReporting,
            accent = SheetAccent.Danger,
            onDismiss = { sheetOpen = false },
            onConfirm = { text ->
                note = text
                sheetOpen = false
                onReport(text)
            },
        )
    }
}
