package cz.cleansia.partner.features.orders

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
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
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import cz.cleansia.core.format.formatOrderTime
import cz.cleansia.core.ui.components.CleansiaButtonSize
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.GetOrderPhotosOrderPhotoDto
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderStatus
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

/**
 * Where the run sheet carries the lockout: from On the way, and on Confirmed once the start has passed;
 * well before the start Getting in says only the mode, floor and flat, and once cleaning has started it is gone.
 */
internal fun OrderItem.showsLockout(now: Instant): Boolean {
    if (isAssignedToCurrentUser != true) return false
    return when (orderStatus.toOrderStatus()) {
        OrderStatus._3 -> true
        OrderStatus._2 -> cleaningStart()?.let { !now.isBefore(it) } == true
        else -> false
    }
}

/** A delay runs on the monotonic clock, which stops while the device sleeps, so the wait is walked in short steps. */
internal fun lockoutClockStep(now: Instant, opensAt: Instant): Long =
    Duration.between(now, opensAt).toMillis().coerceIn(0L, 30_000L)

/**
 * The lockout inside *Getting in*: before it opens one line, *Cannot get in? … you can report it from
 * 14:15 …*; open, the entrance photo and *I cannot get in*; reported, the report as written. Each says that a
 * confirmed lockout is paid in full.
 */
@Composable
internal fun LockoutCard(
    order: OrderItem,
    now: Instant,
    entrancePhotos: List<GetOrderPhotosOrderPhotoDto>,
    mutation: PhotoMutationState,
    isReporting: Boolean,
    actionsEnabled: Boolean,
    onReport: (String) -> Unit,
    onAddEntrancePhoto: () -> Unit,
    onDeletePhoto: (String) -> Unit,
) {
    var clock by remember(now) { mutableStateOf(now) }
    val standing = order.lockoutStanding(clock)
    if (standing is LockoutStanding.NotYet) {
        LaunchedEffect(standing.opensAt) {
            while (Instant.now().isBefore(standing.opensAt)) {
                delay(lockoutClockStep(Instant.now(), standing.opensAt))
            }
            clock = Instant.now()
        }
    }
    val paid = stringResource(R.string.lockout_paid_in_full)

    when (standing) {
        LockoutStanding.Hidden -> Unit
        is LockoutStanding.NotYet -> Text(
            text = buildAnnotatedString {
                withStyle(SpanStyle(color = MaterialTheme.colorScheme.onSurface)) {
                    append(stringResource(R.string.lockout_card_title))
                }
                append(" ")
                append(stringResource(R.string.lockout_card_not_yet, formatOrderTime(standing.opensAt.toString()), LOCKOUT_WAIT_MINUTES))
                append(" ")
                append(OrdersBoardRules.tie(paid))
            },
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        LockoutStanding.Open -> OpenLockoutCard(
            entrancePhotos = entrancePhotos,
            mutation = mutation,
            isReporting = isReporting,
            actionsEnabled = actionsEnabled,
            onReport = onReport,
            onAddEntrancePhoto = onAddEntrancePhoto,
            onDeletePhoto = onDeletePhoto,
        )
        is LockoutStanding.Reported -> Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            PartHeading(stringResource(R.string.lockout_reported_title))
            Text(
                text = OrdersBoardRules.tie(stringResource(R.string.lockout_reported_body, formatOrderTime(standing.reportedAt))),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
            )
            standing.callAttempts?.takeIf { it.isNotBlank() }?.let { calls ->
                val label = stringResource(R.string.lockout_reported_calls, "")
                Text(
                    text = buildAnnotatedString {
                        append(label)
                        append(customerText(calls))
                    },
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            if (entrancePhotos.isNotEmpty()) {
                PhotoGroup(stringResource(R.string.lockout_entrance_photo), entrancePhotos, mutation, false, null, {}, onDeletePhoto)
            }
            Text(text = OrdersBoardRules.tie(paid), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface)
        }
    }
}

@Composable
private fun OpenLockoutCard(
    entrancePhotos: List<GetOrderPhotosOrderPhotoDto>,
    mutation: PhotoMutationState,
    isReporting: Boolean,
    actionsEnabled: Boolean,
    onReport: (String) -> Unit,
    onAddEntrancePhoto: () -> Unit,
    onDeletePhoto: (String) -> Unit,
) {
    var sheetOpen by remember { mutableStateOf(false) }
    var note by rememberSaveable { mutableStateOf("") }

    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        PartHeading(stringResource(R.string.lockout_card_title))
        Text(
            text = OrdersBoardRules.tie(stringResource(R.string.lockout_card_body) + " " + stringResource(R.string.lockout_paid_in_full)),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
        )
        PhotoGroup(
            title = stringResource(R.string.lockout_entrance_photo),
            photos = entrancePhotos,
            mutation = mutation,
            canUpload = true,
            slotNote = stringResource(R.string.lockout_photo_needed),
            onAdd = onAddEntrancePhoto,
            onDelete = onDeletePhoto,
        )
        CleansiaOutlinedButton(
            text = stringResource(R.string.lockout_report_action),
            onClick = { sheetOpen = true },
            size = CleansiaButtonSize.Medium,
            enabled = entrancePhotos.isNotEmpty() && actionsEnabled && !isReporting,
        )
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
