package cz.cleansia.partner.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.PhotoCamera
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderStatus

/**
 * Single-button (or button-pair) primary action area for the order
 * details screen.
 *
 * Status × ownership → action:
 *   New / Confirmed, NOT mine:   Slide-to-take
 *   Confirmed (mine):            "Notify on the way" (single button)
 *   OnTheWay (mine):             Slide-to-start
 *   InProgress (mine):           "Mark cash collected" (unpaid cash) then Slide-to-complete
 *   Completed / Cancelled:       (nothing)
 *
 * Phase B added `isAssignedToCurrentUser` on the detail DTO, so we no
 * longer need the "render Take and let the server reject" hack from
 * Phase A — Take only appears on offers the cleaner could actually
 * accept.
 */
@Composable
fun OrderPrimaryAction(
    status: OrderStatus?,
    isAssignedToCurrentUser: Boolean,
    inFlight: OrderAction?,
    onTake: () -> Unit,
    onStart: () -> Unit,
    onNotifyOnTheWay: () -> Unit,
    onCompleteClick: () -> Unit,
    onMarkCashCollected: () -> Unit,
    canComplete: Boolean = true,
    needsCashCollection: Boolean = false,
    modifier: Modifier = Modifier,
) {
    when (status) {
        OrderStatus._0, OrderStatus._2 -> {
            if (!isAssignedToCurrentUser) {
                // Available offer the cleaner could take.
                SlideToCommit(
                    idleLabel = stringResource(R.string.slide_to_take),
                    busyLabel = stringResource(R.string.taking_order),
                    onCommit = onTake,
                    isBusy = inFlight == OrderAction.Take,
                    modifier = modifier,
                )
            } else if (status == OrderStatus._2) {
                // Confirmed and assigned to me — show the expected next
                // step. We drop the parallel "Start now" shortcut from
                // Phase A: the OnTheWay step has its own slide-to-start
                // and skipping it via a button conflicts with the
                // server's expected lifecycle (OnTheWay event drives the
                // customer's tracking screen).
                CleansiaPrimaryButton(
                    text = stringResource(R.string.notify_on_the_way),
                    onClick = onNotifyOnTheWay,
                    loading = inFlight == OrderAction.NotifyOnTheWay,
                    enabled = inFlight == null,
                    modifier = modifier,
                )
            }
            // status == _0 + assigned: nothing — server lifecycle should
            // have moved it to _2 the moment we took it. If we somehow
            // get here, no action is the safe default.
        }
        OrderStatus._3 -> {
            // OnTheWay → confirm arrival with a deliberate gesture.
            // Foreign OnTheWay orders shouldn't be returned by the
            // browse-detail endpoint, but render nothing as a safety
            // net so we never offer a stranger's order to Start.
            if (isAssignedToCurrentUser) {
                SlideToCommit(
                    idleLabel = stringResource(R.string.slide_to_start),
                    busyLabel = stringResource(R.string.starting_order),
                    onCommit = onStart,
                    isBusy = inFlight == OrderAction.Start,
                    modifier = modifier,
                )
            }
        }
        OrderStatus._4 -> {
            // InProgress → wrap up via a deliberate slide gesture.
            // Same SlideToCommit primitive as Take + Start so the
            // three terminal actions feel symmetrical. We dropped the
            // old CompleteOrderDialog entirely — actual-time and
            // completion-notes are both optional on the backend.
            if (isAssignedToCurrentUser) {
                if (canComplete) {
                    if (needsCashCollection) {
                        // Cash order still unpaid — the server rejects
                        // CompleteOrder until the cleaner records the cash.
                        // Swap the complete slide for this button; marking
                        // cash flips the order to Paid and the slide returns.
                        CleansiaPrimaryButton(
                            text = stringResource(R.string.order_mark_cash_collected),
                            onClick = onMarkCashCollected,
                            loading = inFlight == OrderAction.MarkCashCollected,
                            enabled = inFlight == null,
                            modifier = modifier,
                        )
                    } else {
                        SlideToCommit(
                            idleLabel = stringResource(R.string.slide_to_complete),
                            busyLabel = stringResource(R.string.completing_order),
                            onCommit = onCompleteClick,
                            isBusy = inFlight == OrderAction.Complete,
                            modifier = modifier,
                        )
                    }
                } else {
                    // After-photos missing — show a soft hint so the
                    // cleaner sees what's blocking the slide. Server
                    // validator stays as a safety net, but this
                    // surfaces the requirement instantly without
                    // round-tripping. Same wording as the backend's
                    // error_key_order_after_photos_required.
                    CompleteBlockedHint(modifier = modifier)
                }
            }
        }
        else -> { /* Completed / Cancelled / null — no actions */ }
    }
}

/**
 * Disabled-state stand-in for the Complete slide. Shown when the
 * cleaner is on InProgress but hasn't uploaded any "after" photo yet
 * (server's [BusinessErrorMessage.AfterPhotosRequired] guard). Uses a
 * muted outline + camera icon + clear copy so the cleaner knows
 * exactly what to do to unlock the slide.
 *
 * Sized to roughly match [SlideToCommit]'s 56dp track so the footer
 * doesn't jump in height when the cleaner uploads a photo and the
 * slide swaps in.
 */
@Composable
private fun CompleteBlockedHint(modifier: Modifier = Modifier) {
    val muted = MaterialTheme.colorScheme.onSurfaceVariant
    Row(
        modifier = modifier
            .fillMaxWidth()
            .background(
                color = MaterialTheme.colorScheme.surfaceVariant,
                shape = RoundedCornerShape(28.dp),
            )
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            imageVector = Icons.Outlined.PhotoCamera,
            contentDescription = null,
            tint = muted,
            modifier = Modifier.size(20.dp),
        )
        Spacer(Modifier.width(12.dp))
        Text(
            text = stringResource(R.string.error_key_order_after_photos_required),
            style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.SemiBold),
            color = muted,
        )
    }
}

