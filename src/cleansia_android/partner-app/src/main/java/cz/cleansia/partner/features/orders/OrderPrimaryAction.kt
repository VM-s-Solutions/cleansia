package cz.cleansia.partner.features.orders

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Lock
import androidx.compose.material.icons.outlined.Phone
import androidx.compose.material.icons.outlined.PhotoCamera
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.FirstBaseline
import androidx.compose.ui.layout.Layout
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.text.rememberTextMeasurer
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.ui.components.LockedSwipeToConfirm
import cz.cleansia.core.ui.components.SwipeToConfirmButton
import cz.cleansia.core.ui.components.slideSemantics
import cz.cleansia.core.ui.theme.NunitoCapHeight
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.OrderStatus
import java.time.Instant
import java.time.ZoneId
import kotlin.math.ceil
import kotlin.math.floor
import kotlin.math.roundToInt

/** The detail's one move under the thumb, decided by [pinnedMove]. */
sealed interface PinnedMove {
    data object None : PinnedMove

    /** Take, or Confirm on a job reserved for her (with Decline beside it). */
    data class Take(val preferred: Boolean) : PinnedMove

    /** Her own rows are still loading: Take waits, so a tap cannot reach the server before the clash check. */
    data class TakeWaiting(val preferred: Boolean) : PinnedMove

    /** The job overlaps one of hers: Take disabled, with the window it hits. */
    data class TakeOverlaps(val clash: OrderListItem) : PinnedMove

    data class SetOffLocked(val opensAt: Instant) : PinnedMove

    /** [late]: the start has passed, so *Already there? Start cleaning* goes under the slide. */
    data class SetOff(val late: Boolean) : PinnedMove
    data object Start : PinnedMove
    data object TakeAfterPhoto : PinnedMove
    data object MarkMyPart : PinnedMove
    data class CallMate(val name: String, val phone: String) : PinnedMove
    data class WaitingFor(val name: String) : PinnedMove
    data class CollectCash(val canReportNotPaid: Boolean) : PinnedMove

    /** A card job Stripe has not charged: `CompleteOrder` refuses it, so the slide is locked with its reason. */
    data object CompleteLocked : PinnedMove
    data object Complete : PinnedMove
}

/** What the server takes from her at [now]; never a control it refuses. */
internal fun pinnedMove(
    status: OrderStatus?,
    isAssignedToCurrentUser: Boolean,
    now: Instant,
    start: Instant?,
    minutes: Int,
    preferred: Boolean = false,
    clash: OrderListItem? = null,
    ownRowsAnswered: Boolean = true,
    completionStep: CompletionStep = CompletionStep.Complete,
    hasAfterPhoto: Boolean = false,
    needsCashCollection: Boolean = false,
    canReportCashNotPaid: Boolean = false,
    cardCharged: Boolean = true,
    waitingFor: CrewSeat? = null,
    mateMarked: Boolean = false,
): PinnedMove {
    val onTake = when {
        clash != null -> PinnedMove.TakeOverlaps(clash)
        !ownRowsAnswered -> PinnedMove.TakeWaiting(preferred)
        else -> PinnedMove.Take(preferred)
    }
    val joinable = start?.plusSeconds(minutes * 60L)?.let { now < it } != false
    return when (status) {
        OrderStatus._0, OrderStatus._2 -> when {
            !isAssignedToCurrentUser -> onTake
            status == OrderStatus._0 -> PinnedMove.None
            start == null -> PinnedMove.SetOff(late = false)
            now < start.minusSeconds(OrdersBoardRules.START_GRACE_WINDOW_MINUTES * 60) ->
                PinnedMove.SetOffLocked(start.minusSeconds(OrdersBoardRules.START_GRACE_WINDOW_MINUTES * 60))
            else -> PinnedMove.SetOff(late = now >= start)
        }
        OrderStatus._3 -> when {
            isAssignedToCurrentUser -> PinnedMove.Start
            joinable -> onTake
            else -> PinnedMove.None
        }
        OrderStatus._4 -> when {
            !isAssignedToCurrentUser -> if (joinable) onTake else PinnedMove.None
            completionStep == CompletionStep.WaitingForCrew -> waitingFor?.let { mate ->
                val name = mate.givenName.orEmpty()
                val phone = mate.callNumber()
                if (phone != null && name.isNotEmpty()) PinnedMove.CallMate(name, phone) else PinnedMove.WaitingFor(name)
            } ?: PinnedMove.None
            completionStep == CompletionStep.MarkMyPart && mateMarked -> PinnedMove.MarkMyPart
            !hasAfterPhoto -> PinnedMove.TakeAfterPhoto
            completionStep == CompletionStep.MarkMyPart -> PinnedMove.MarkMyPart
            needsCashCollection -> PinnedMove.CollectCash(canReportCashNotPaid)
            !cardCharged -> PinnedMove.CompleteLocked
            else -> PinnedMove.Complete
        }
        else -> PinnedMove.None
    }
}

/**
 * The pinned move under the thumb, on the app's controls: the Core slide in its lanes, its locked face for a move that
 * is not open yet, the Core buttons; a Take the server would refuse says why above the disabled button.
 */
@Composable
fun OrderPrimaryAction(
    move: PinnedMove,
    inFlight: OrderAction?,
    now: Instant,
    cashDue: String?,
    onTake: () -> Unit,
    onDeclineOffer: () -> Unit,
    onStart: () -> Unit,
    onStartRequested: () -> Unit,
    onNotifyOnTheWay: () -> Unit,
    onCompleteClick: () -> Unit,
    onMarkMyPartDone: () -> Unit,
    onCashConfirmRequested: () -> Unit,
    onCashNotPaidRequested: () -> Unit,
    onTakeAfterPhoto: () -> Unit,
    onCallMate: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    when (move) {
        PinnedMove.None -> Unit
        is PinnedMove.Take -> if (move.preferred) {
            OfferPair(declineEnabled = inFlight == null, onDecline = onDeclineOffer, modifier = modifier) { slot ->
                TakeButton(isPreferredOffer = true, inFlight = inFlight, onTake = onTake, modifier = slot)
            }
        } else {
            TakeButton(isPreferredOffer = false, inFlight = inFlight, onTake = onTake, modifier = modifier)
        }
        is PinnedMove.TakeWaiting -> if (move.preferred) {
            OfferPair(declineEnabled = inFlight == null, onDecline = onDeclineOffer, modifier = modifier) { slot ->
                CleansiaPrimaryButton(text = stringResource(R.string.offer_confirm), onClick = {}, loading = true, modifier = slot)
            }
        } else {
            CleansiaPrimaryButton(text = stringResource(R.string.take_order), onClick = {}, loading = true, modifier = modifier)
        }
        is PinnedMove.TakeOverlaps -> {
            val reason = stringResource(
                R.string.orders_flag_overlap,
                OrdersBoardRules.window(OrdersBoardRules.start(move.clash), OrdersBoardRules.minutes(move.clash), zone, locale).orEmpty(),
            )
            Column(modifier.fillMaxWidth()) {
                OverlapReason(OrdersBoardRules.tie(reason), Modifier.fillMaxWidth().padding(bottom = 10.dp))
                CleansiaPrimaryButton(
                    text = stringResource(R.string.take_order),
                    onClick = {},
                    enabled = false,
                    disabledContainerColor = MaterialTheme.colorScheme.surfaceVariant,
                    disabledContentColor = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.semantics { stateDescription = reason },
                )
            }
        }
        is PinnedMove.SetOffLocked -> {
            val reason = OrdersBoardRules.tie(stringResource(R.string.active_set_off_from, OrdersBoardRules.timeText(move.opensAt, zone, locale)))
            LockedSwipeToConfirm(
                reason = reason,
                modifier = modifier.slideSemantics(label = reason, busyLabel = reason, enabled = false, isBusy = false, onCommit = {}, disabledReason = reason),
            )
        }
        is PinnedMove.SetOff -> Column(modifier.fillMaxWidth()) {
            if (inFlight == OrderAction.Start) {
                DetailSlide(
                    idle = stringResource(R.string.slide_to_start),
                    busy = stringResource(R.string.starting_order),
                    isBusy = true,
                    onCommit = onStart,
                )
            } else {
                DetailSlide(
                    idle = stringResource(R.string.swipe_to_notify_on_the_way),
                    busy = stringResource(R.string.customer_notified_on_the_way),
                    isBusy = inFlight == OrderAction.NotifyOnTheWay,
                    onCommit = onNotifyOnTheWay,
                )
                if (move.late) {
                    Box(Modifier.fillMaxWidth().padding(top = 4.dp), contentAlignment = Alignment.Center) {
                        CleansiaTextLink(text = stringResource(R.string.late_already_there), onClick = onStartRequested, enabled = inFlight == null)
                    }
                }
            }
        }
        PinnedMove.Start -> DetailSlide(
            idle = stringResource(R.string.slide_to_start),
            busy = stringResource(R.string.starting_order),
            isBusy = inFlight == OrderAction.Start,
            onCommit = onStart,
            modifier = modifier,
        )
        PinnedMove.TakeAfterPhoto -> CleansiaPrimaryButton(
            text = stringResource(R.string.take_after_photo),
            onClick = onTakeAfterPhoto,
            leadingIcon = Icons.Outlined.PhotoCamera,
            enabled = inFlight == null,
            modifier = modifier,
        )
        PinnedMove.MarkMyPart -> DetailSlide(
            idle = stringResource(R.string.slide_to_mark_my_part_done),
            busy = stringResource(R.string.marking_my_part_done),
            isBusy = inFlight == OrderAction.MarkMyPartDone,
            onCommit = onMarkMyPartDone,
            detail = stringResource(R.string.crew_mark_done_sub),
            modifier = modifier,
        )
        is PinnedMove.CallMate -> CleansiaOutlinedButton(
            text = "${stringResource(R.string.action_call)}${OrdersBoardRules.SEP}${move.name}",
            onClick = { onCallMate(move.phone) },
            modifier = modifier,
            leadingIcon = Icons.Outlined.Phone,
            contentColor = primaryText(),
            borderColor = MaterialTheme.colorScheme.primary,
        )
        is PinnedMove.WaitingFor -> Text(
            text = OrdersBoardRules.tie(stringResource(R.string.detail_waiting_for, move.name)),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = modifier.fillMaxWidth().padding(vertical = 16.dp),
        )
        is PinnedMove.CollectCash -> Column(modifier.fillMaxWidth()) {
            // The only irreversible money action in the app: the tap asks, and the screen's confirm sends it.
            CleansiaPrimaryButton(
                text = cashDue?.let { stringResource(R.string.cash_mark_amount, it) } ?: stringResource(R.string.order_mark_cash_collected),
                onClick = onCashConfirmRequested,
                loading = inFlight == OrderAction.MarkCashCollected,
                enabled = inFlight == null,
            )
            if (move.canReportNotPaid) {
                CashNotPaidButton(
                    reporting = inFlight == OrderAction.ReportCashNotPaid,
                    enabled = inFlight == null,
                    onClick = onCashNotPaidRequested,
                )
            }
        }
        PinnedMove.CompleteLocked -> {
            val reason = stringResource(R.string.error_order_payment_not_confirmed)
            LockedSwipeToConfirm(
                reason = reason,
                modifier = modifier.slideSemantics(label = reason, busyLabel = reason, enabled = false, isBusy = false, onCommit = {}, disabledReason = reason),
            )
        }
        PinnedMove.Complete -> DetailSlide(
            idle = stringResource(R.string.slide_to_complete),
            busy = stringResource(R.string.completing_order),
            isBusy = inFlight == OrderAction.Complete,
            onCommit = onCompleteClick,
            modifier = modifier,
        )
    }
}

/** The detail's slide: the Core slide in its lanes, the label on up to two lines, what it records under it. */
@Composable
private fun DetailSlide(idle: String, busy: String, isBusy: Boolean, onCommit: () -> Unit, modifier: Modifier = Modifier, detail: String? = null) {
    SwipeToConfirmButton(
        text = idle,
        onConfirmed = onCommit,
        modifier = modifier.slideSemantics(label = listOfNotNull(idle, detail).joinToString(", "), busyLabel = busy, enabled = true, isBusy = isBusy, onCommit = onCommit),
        isBusy = isBusy,
        busyText = busy,
        maxLines = 2,
        thumbLanes = true,
        detail = detail,
    )
}

@Composable
private fun OfferPair(declineEnabled: Boolean, onDecline: () -> Unit, modifier: Modifier = Modifier, confirm: @Composable (Modifier) -> Unit) {
    val decline: @Composable (Modifier) -> Unit = { slot ->
        CleansiaOutlinedButton(
            text = stringResource(R.string.offer_decline),
            onClick = onDecline,
            modifier = slot,
            enabled = declineEnabled,
            contentColor = primaryText(),
            borderColor = MaterialTheme.colorScheme.primary,
        )
    }
    if (LocalDensity.current.fontScale >= 1.3f) {
        Column(modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            confirm(Modifier.fillMaxWidth())
            decline(Modifier.fillMaxWidth())
        }
    } else {
        Row(modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            decline(Modifier.weight(1f))
            confirm(Modifier.weight(1f))
        }
    }
}

@Composable
private fun CashNotPaidButton(reporting: Boolean, enabled: Boolean, onClick: () -> Unit) {
    Box(modifier = Modifier.fillMaxWidth(), contentAlignment = Alignment.Center) {
        if (reporting) {
            CircularProgressIndicator(modifier = Modifier.padding(vertical = 12.dp).size(20.dp), strokeWidth = 2.dp)
        } else {
            CleansiaTextLink(text = stringResource(R.string.order_cash_not_paid_action), onClick = onClick, enabled = enabled)
        }
    }
}

/**
 * Opens the contract sheet for the take. Spins while the host reconciles the order after the
 * sheet's verdict, so the button cannot reopen the sheet on a job that is already taken.
 */
@Composable
private fun TakeButton(
    isPreferredOffer: Boolean,
    inFlight: OrderAction?,
    onTake: () -> Unit,
    modifier: Modifier = Modifier,
) {
    CleansiaPrimaryButton(
        text = stringResource(if (isPreferredOffer) R.string.offer_confirm else R.string.take_order),
        onClick = onTake,
        loading = inFlight == OrderAction.Take,
        enabled = inFlight == null,
        modifier = modifier,
    )
}

internal fun overlapReasonRoomPx(roomPx: Int, lockWidthPx: Int, gapPx: Int): Int =
    if (roomPx == Constraints.Infinity) Constraints.Infinity else (roomPx - lockWidthPx - gapPx).coerceAtLeast(0)

/**
 * Why the Take is refused, centred, its lock just before the first word of the first line: on one line the two are
 * centred together, and a wrapped reason keeps its lines centred on one axis with the lock on its first.
 */
@Composable
private fun OverlapReason(text: String, modifier: Modifier = Modifier) {
    val style = MaterialTheme.typography.bodyMedium.copy(textAlign = TextAlign.Center)
    val measurer = rememberTextMeasurer()
    val capHeightPx = with(LocalDensity.current) { style.fontSize.toPx() } * NunitoCapHeight
    Layout(
        content = {
            Icon(Icons.Outlined.Lock, contentDescription = null, tint = MaterialTheme.colorScheme.error, modifier = Modifier.size(16.dp))
            Text(text = text, style = style, color = MaterialTheme.colorScheme.error)
        },
        modifier = modifier,
    ) { measurables, constraints ->
        val lock = measurables[0].measure(Constraints())
        val gap = 6.dp.roundToPx()
        val room = Constraints(maxWidth = overlapReasonRoomPx(constraints.maxWidth, lock.width, gap))
        val words = measurables[1].measure(room)
        val width = if (constraints.hasBoundedWidth) constraints.maxWidth else lock.width + gap + words.width
        val first = measurer.measure(text, style, constraints = room).let { floor(it.getLineLeft(0)).toInt() to ceil(it.getLineRight(0)).toInt() }
        val lockX = ((width - lock.width - gap - (first.second - first.first)) / 2f).roundToInt()
        val wordsX = lockX + lock.width + gap - first.first
        val lockY = words[FirstBaseline] - (lock.height / 2f + capHeightPx / 2f).roundToInt()
        val top = minOf(0, lockY)
        val height = maxOf(words.height, lockY + lock.height) - top
        layout(width, height.coerceIn(constraints.minHeight, constraints.maxHeight)) {
            lock.place(lockX, lockY - top)
            words.place(wordsX, -top)
        }
    }
}
