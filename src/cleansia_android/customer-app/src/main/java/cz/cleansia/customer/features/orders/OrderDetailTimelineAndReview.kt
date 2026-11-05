package cz.cleansia.customer.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.Star
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.OrderSectionHeader
import cz.cleansia.core.ui.theme.orderStatusColor
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.customer.R
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderStatusTrackDto
import cz.cleansia.customer.ui.theme.WarningStar

/* ── Timeline ── */

internal data class TimelineEntry(val track: OrderStatusTrackDto, val returnedToBoard: Boolean)

/**
 * The history oldest first, a row with no time last, each status listed once per pass: a seat
 * advertised again writes a row repeating the status the booking is already in.
 */
internal fun timelineRows(history: List<OrderStatusTrackDto>): List<TimelineEntry> {
    val rows = mutableListOf<TimelineEntry>()
    history.sortedWith(compareBy(nullsLast()) { it.createdOn }).forEach { track ->
        val previous = rows.lastOrNull()?.track?.status?.value
        if (rows.isNotEmpty() && track.status?.value == previous) return@forEach
        // A New after a Confirmed is the walk-back, the one Confirmed → New writer. → /domain/order-lifecycle
        val returnedToBoard = orderStatusFromValue(track.status?.value) == OrderStatus.New &&
            orderStatusFromValue(previous) == OrderStatus.Confirmed
        rows += TimelineEntry(track, returnedToBoard)
    }
    return rows
}

@Composable
internal fun TimelineCard(history: List<OrderStatusTrackDto>) {
    val rows = timelineRows(history)
    Card {
        OrderSectionHeader(title = stringResource(R.string.order_detail_timeline))
        Spacer(Modifier.height(10.dp))
        rows.forEachIndexed { idx, row ->
            TimelineRow(row.track, returnedToBoard = row.returnedToBoard, isLast = idx == rows.lastIndex)
        }
    }
}

@Composable
private fun TimelineRow(entry: OrderStatusTrackDto, returnedToBoard: Boolean, isLast: Boolean) {
    val dotColor = orderStatusColor(entry.status?.value)
    Row(Modifier.padding(vertical = 4.dp)) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Box(
                Modifier
                    .size(12.dp)
                    .background(dotColor, CircleShape),
            )
            if (!isLast) {
                Box(
                    Modifier
                        .width(2.dp)
                        .height(28.dp)
                        .background(MaterialTheme.colorScheme.outlineVariant),
                )
            }
        }
        Spacer(Modifier.width(12.dp))
        Column(Modifier.padding(bottom = if (isLast) 0.dp else 4.dp)) {
            // Resolve a localized label per status value, falling back to the
            // raw `name` from the CodeDto (English) when the value is unknown
            // (e.g. backend adds a new status before mobile updates).
            val labelRes = if (returnedToBoard) {
                R.string.order_detail_timeline_new_cleaner
            } else {
                orderStatusLabelRes(entry.status?.value)
            }
            val label = labelRes?.let { stringResource(it) } ?: entry.status?.name ?: "—"
            Text(
                text = label,
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Text(
                text = formatOrderDateTime(entry.createdOn),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

/* ── Review ── */

@OptIn(androidx.compose.foundation.layout.ExperimentalLayoutApi::class)
@Composable
internal fun ReviewCard(
    order: OrderDetailDto,
    onLeaveReview: () -> Unit,
) {
    Card {
        OrderSectionHeader(title = stringResource(R.string.order_detail_your_review))
        Spacer(Modifier.height(8.dp))
        val review = order.review
        if (review != null) {
            androidx.compose.foundation.layout.FlowRow(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalArrangement = Arrangement.spacedBy(4.dp)) {
                val ratingLabel = stringResource(R.string.order_review_stars_accessibility, review.rating)
                Row(Modifier.align(Alignment.CenterVertically).semantics { contentDescription = ratingLabel }) {
                    repeat(5) { idx ->
                        Icon(Icons.Outlined.Star, contentDescription = null,
                            tint = if (idx < review.rating) WarningStar else MaterialTheme.colorScheme.outlineVariant,
                            modifier = Modifier.size(22.dp))
                    }
                }
                cz.cleansia.core.ui.components.CleansiaTextButton(
                    onClick = onLeaveReview,
                    modifier = Modifier.align(Alignment.CenterVertically),
                ) {
                    Text(stringResource(R.string.order_review_edit_action), maxLines = 1, overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis)
                }
            }
            review.comment?.takeIf { it.isNotBlank() }?.let { comment ->
                Spacer(Modifier.height(6.dp))
                Text(
                    text = "“$comment”",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        } else {
            // Wave 2 Phase 3 — the CTA is live. Filled primary button, full-
            // width, opens the SubmitReviewSheet via the parent's callback.
            Button(
                onClick = onLeaveReview,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(48.dp),
                shape = CircleShape,
                colors = ButtonDefaults.buttonColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    contentColor = MaterialTheme.colorScheme.onPrimary,
                ),
            ) {
                Text(
                    text = stringResource(R.string.order_detail_leave_review),
                    style = MaterialTheme.typography.titleMedium,
                )
            }
        }
    }
}

/* ── Receipt ── */

/**
 * Download-receipt card. Active when `order.receiptNumber` is non-blank; shows
 * a disabled button + "not ready yet" caption otherwise so the card can still
 * render for Completed orders whose backend receipt number hasn't caught up.
 *
 * Loading pattern mirrors the Phase 2 cancel button — inline spinner replaces
 * the button label while the download is in flight.
 */
@Composable
internal fun ReceiptCard(
    order: OrderDetailDto,
    onDownload: () -> Unit,
    isDownloading: Boolean,
) {
    val hasReceipt = !order.receiptNumber.isNullOrBlank()
    Card {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(
                Icons.Outlined.Description,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
            Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    text = stringResource(R.string.order_detail_download_receipt),
                    style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                order.receiptNumber?.takeIf { it.isNotBlank() }?.let { num ->
                    Text(
                        text = num,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
            Spacer(Modifier.width(8.dp))
            Button(
                onClick = onDownload,
                enabled = hasReceipt && !isDownloading,
                shape = CircleShape,
                colors = ButtonDefaults.buttonColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    contentColor = MaterialTheme.colorScheme.onPrimary,
                ),
                modifier = Modifier.height(40.dp),
            ) {
                if (isDownloading) {
                    CircularProgressIndicator(
                        color = MaterialTheme.colorScheme.onPrimary,
                        strokeWidth = 2.dp,
                        modifier = Modifier.size(16.dp),
                    )
                } else {
                    Text(
                        text = stringResource(R.string.order_detail_download_receipt),
                        style = MaterialTheme.typography.labelLarge,
                    )
                }
            }
        }
        if (!hasReceipt) {
            Spacer(Modifier.height(8.dp))
            Text(
                text = stringResource(R.string.order_receipt_not_ready),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}
