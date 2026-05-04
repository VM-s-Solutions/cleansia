package cz.cleansia.customer.features.booking

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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.customer.R
import cz.cleansia.customer.core.format.formatOrderDateRange
import cz.cleansia.customer.core.format.formatOrderPrice
import cz.cleansia.customer.core.orders.OrderAddressDto
import cz.cleansia.customer.core.orders.OrderCurrencyDetailDto
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.features.orders.OrderStatus
import cz.cleansia.customer.features.orders.orderStatusFromValue
import cz.cleansia.customer.ui.components.CleansiaOutlinedButton
import cz.cleansia.customer.ui.components.CleansiaPrimaryButton
import cz.cleansia.customer.ui.components.MascotAnimation
import cz.cleansia.customer.ui.theme.CleansiaTheme

/**
 * State of a single timeline row. `Done` rows are checked off, `Active`
 * is the row the order is currently sitting on (highlighted), `Pending`
 * is everything ahead.
 */
private enum class StepState { Done, Active, Pending }

private data class TimelineStep(val titleRes: Int, val descRes: Int, val state: StepState)

/**
 * Map a loaded order's status (and whether a cleaner has been assigned)
 * to the 4-step booking-success timeline. The mapping:
 *   t1 Booking received  → always Done (the order exists)
 *   t2 Cleaner assigned  → Active until status >= Confirmed; Done after
 *   t3 Cleaner confirmed → Active when Confirmed/InProgress; Done after Completed
 *   t4 Cleaning day      → Active when InProgress; Done when Completed
 *
 * Falls back to a "just placed" view (only t1 done, t2 active) if the
 * order detail hasn't loaded yet — matches what the user expects right
 * after submission.
 */
private fun computeTimelineSteps(order: OrderDetailDto?): List<TimelineStep> {
    val status = orderStatusFromValue(order?.orderStatus?.value)
    val cleanerAssigned = !order?.assignedEmployees.isNullOrEmpty()

    val t2State = when (status) {
        // No order loaded yet — assume backend is matching, this row is active.
        null -> StepState.Active
        // Brand new / awaiting payment / unmatched: still searching for a cleaner.
        OrderStatus.New, OrderStatus.Pending -> {
            if (cleanerAssigned) StepState.Done else StepState.Active
        }
        // Cleaner has accepted or work has begun — assignment phase is over.
        OrderStatus.Confirmed, OrderStatus.InProgress, OrderStatus.Completed -> StepState.Done
        OrderStatus.Cancelled -> StepState.Done
    }

    val t3State = when (status) {
        null, OrderStatus.New, OrderStatus.Pending -> StepState.Pending
        OrderStatus.Confirmed -> StepState.Active
        OrderStatus.InProgress, OrderStatus.Completed -> StepState.Done
        OrderStatus.Cancelled -> StepState.Pending
    }

    val t4State = when (status) {
        OrderStatus.InProgress -> StepState.Active
        OrderStatus.Completed -> StepState.Done
        else -> StepState.Pending
    }

    return listOf(
        TimelineStep(R.string.booking_success_t1_title, R.string.booking_success_t1_desc, StepState.Done),
        TimelineStep(R.string.booking_success_t2_title, R.string.booking_success_t2_desc, t2State),
        TimelineStep(R.string.booking_success_t3_title, R.string.booking_success_t3_desc, t3State),
        TimelineStep(R.string.booking_success_t4_title, R.string.booking_success_t4_desc, t4State),
    )
}

@Composable
fun BookingSuccessScreen(
    confirmationCode: String,
    orderId: String,
    onViewOrders: () -> Unit = {},
    onGoHome: () -> Unit = {},
    viewModel: BookingSuccessViewModel = hiltViewModel(),
) {
    val uiState by viewModel.state.collectAsStateWithLifecycle()

    // Prefer the freshly-loaded confirmation code if it differs from the nav arg
    // (e.g. backend trims whitespace). Falls back to the nav-arg value so the
    // pill still renders in Loading/Error states.
    val loadedOrder = (uiState as? BookingSuccessUiState.Loaded)?.order
    val effectiveCode = loadedOrder?.confirmationCode?.takeIf { it.isNotBlank() }
        ?: confirmationCode

    // Timeline reflects the actual order's status + cleaner assignment.
    // Before the detail loads, falls back to a "just placed, searching for a
    // cleaner" view — matches what the user expects in the moment.
    val steps = computeTimelineSteps(loadedOrder)

    // Compact layout — every section trimmed by ~30% so the full success
    // sequence (mascot, code, summary, timeline, CTAs) fits on a single
    // mid-range device viewport without scrolling. Still verticalScroll-wrapped
    // for safety on small/landscape screens.
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .verticalScroll(rememberScrollState()),
        contentAlignment = Alignment.Center,
    ) { Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp, vertical = 16.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        // Mascot — short welcoming animation. Played via Coil's animated WebP
        // decoder so the alpha channel renders correctly over the background.
        // Plays exactly once and freezes on the final frame — repeating it
        // would feel needy for a one-shot success moment.
        MascotAnimation(
            resId = R.raw.mascot_welcoming,
            size = 220.dp,
            loop = false,
        )
        Spacer(Modifier.height(8.dp))

        Text(
            stringResource(R.string.booking_success_title),
            style = MaterialTheme.typography.headlineSmall,
            color = MaterialTheme.colorScheme.onBackground,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(4.dp))
        Text(
            stringResource(R.string.booking_success_subtitle),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )

        if (effectiveCode.isNotBlank()) {
            Spacer(Modifier.height(14.dp))
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(14.dp))
                    .background(MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.35f))
                    .border(1.dp, MaterialTheme.colorScheme.primary, RoundedCornerShape(14.dp))
                    .padding(vertical = 10.dp, horizontal = 16.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Text(
                    stringResource(R.string.booking_success_confirmation_code),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Spacer(Modifier.height(2.dp))
                androidx.compose.foundation.text.selection.SelectionContainer {
                    Text(
                        effectiveCode,
                        style = MaterialTheme.typography.headlineSmall.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.primary,
                        textAlign = TextAlign.Center,
                    )
                }
            }
        }

        // Enrichment block — state-dependent.
        // Loading: small spinner in place of rows; Loaded: real rows; Error: skipped.
        when (val s = uiState) {
            is BookingSuccessUiState.Loading -> {
                Spacer(Modifier.height(12.dp))
                CircularProgressIndicator(
                    modifier = Modifier.size(24.dp),
                    strokeWidth = 2.5.dp,
                    color = MaterialTheme.colorScheme.primary,
                )
            }
            is BookingSuccessUiState.Loaded -> {
                Spacer(Modifier.height(12.dp))
                OrderSummaryCard(order = s.order)
            }
            is BookingSuccessUiState.Error -> Unit
        }

        Spacer(Modifier.height(14.dp))

        // Timeline tracking — vertical with connected dots, current step pulsing
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(14.dp))
                .background(MaterialTheme.colorScheme.surface)
                .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
                .padding(12.dp),
        ) {
            Text(
                stringResource(R.string.booking_success_progress),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onBackground,
            )
            Spacer(Modifier.height(8.dp))
            steps.forEachIndexed { idx, step ->
                TimelineStepRow(step, isLast = idx == steps.lastIndex)
            }
        }

        Spacer(Modifier.height(10.dp))

        // Static "what's next" note — same copy regardless of fetch state.
        Text(
            stringResource(R.string.booking_success_whats_next),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )

        Spacer(Modifier.height(14.dp))

        CleansiaPrimaryButton(
            text = stringResource(R.string.booking_success_view_orders),
            onClick = onViewOrders,
        )
        Spacer(Modifier.height(6.dp))
        CleansiaOutlinedButton(
            text = stringResource(R.string.booking_success_go_home),
            onClick = onGoHome,
        )
    } }
}

/**
 * Summary card shown on Loaded state — arrival window, address and total.
 * Each row is defensive against missing fields: rows with blank values
 * drop out, and the card as a whole is only rendered when there's at least
 * one populated row (the caller guarantees state == Loaded, but the backend
 * could still return a partially-populated order).
 */
@Composable
private fun OrderSummaryCard(order: OrderDetailDto) {
    val arrival = formatOrderDateRange(order.cleaningDateTime, order.estimatedTime)
        .takeIf { it != "—" }
    val address = formatAddressLine(order.address)
    val total = formatTotalLine(order.totalPrice, order.currency)

    // Nothing to show — skip the card entirely rather than render an empty frame.
    if (arrival == null && address.isNullOrBlank() && total.isNullOrBlank()) return

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
            .padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        if (arrival != null) {
            SummaryRow(
                label = stringResource(R.string.booking_success_arrival_label),
                value = arrival,
            )
        }
        if (!address.isNullOrBlank()) {
            SummaryRow(
                label = stringResource(R.string.booking_success_address_label),
                value = address,
            )
        }
        if (!total.isNullOrBlank()) {
            SummaryRow(
                label = stringResource(R.string.booking_success_total_label),
                value = total,
            )
        }
    }
}

@Composable
private fun SummaryRow(label: String, value: String) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.Top,
    ) {
        Text(
            label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.width(12.dp))
        Text(
            value,
            style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
            textAlign = TextAlign.End,
        )
    }
}

private fun formatAddressLine(address: OrderAddressDto?): String? {
    if (address == null) return null
    return listOfNotNull(
        address.street?.takeIf { it.isNotBlank() },
        address.city?.takeIf { it.isNotBlank() },
    ).joinToString(", ").takeIf { it.isNotBlank() }
}

private fun formatTotalLine(totalPrice: Double, currency: OrderCurrencyDetailDto?): String? {
    if (totalPrice <= 0.0) return null
    return formatOrderPrice(totalPrice, currency?.code)
}

@Composable
private fun TimelineStepRow(step: TimelineStep, isLast: Boolean) {
    val primary = MaterialTheme.colorScheme.primary
    val muted = MaterialTheme.colorScheme.outlineVariant
    val onPrimary = MaterialTheme.colorScheme.onPrimary

    val (dotColor, connectorColor) = when (step.state) {
        StepState.Done -> primary to primary
        StepState.Active -> primary to muted
        StepState.Pending -> muted to muted
    }

    Row(verticalAlignment = Alignment.Top) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Box(
                modifier = Modifier
                    .size(if (step.state == StepState.Done) 14.dp else 12.dp)
                    .background(dotColor, CircleShape),
                contentAlignment = Alignment.Center,
            ) {
                if (step.state == StepState.Done) {
                    Icon(
                        imageVector = Icons.Filled.Check,
                        contentDescription = null,
                        tint = onPrimary,
                        modifier = Modifier.size(10.dp),
                    )
                }
            }
            if (!isLast) {
                Box(
                    Modifier
                        .width(2.dp)
                        .height(22.dp)
                        .background(connectorColor),
                )
            }
        }
        Spacer(Modifier.width(10.dp))
        Column(Modifier.padding(bottom = if (isLast) 0.dp else 8.dp)) {
            Text(
                stringResource(step.titleRes),
                style = MaterialTheme.typography.bodyMedium.copy(
                    fontWeight = if (step.state == StepState.Active) FontWeight.SemiBold else FontWeight.Normal,
                ),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Text(
                stringResource(step.descRes),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Preview(widthDp = 390, heightDp = 1000)
@Composable
private fun BookingSuccessPreview() {
    CleansiaTheme {
        // Preview can't instantiate a Hilt VM — the screen will render the
        // Loading branch by default, which is a reasonable preview snapshot.
        BookingSuccessScreen(
            confirmationCode = "ABC-123-XYZ",
            orderId = "00000000-0000-0000-0000-000000000000",
        )
    }
}
