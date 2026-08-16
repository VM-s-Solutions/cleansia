package cz.cleansia.partner.features.orders

import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import cz.cleansia.core.ui.components.OrderTrackerBar
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderStatus

/**
 * The partner's five-phase tracker. The bar itself is [OrderTrackerBar] in `:core`; this maps the
 * generated [OrderStatus] onto its step index and supplies the translated labels.
 *
 * The customer app draws the same bar off its own status enum — that split is why the shared component
 * takes an index rather than a status.
 */
@Composable
fun ContinuousProgressBar(
    status: OrderStatus?,
    modifier: Modifier = Modifier,
) {
    val currentStep = when (status) {
        OrderStatus._2 -> 1
        OrderStatus._3 -> 2
        OrderStatus._4 -> 3
        OrderStatus._5 -> 4
        else -> 0
    }
    // Completed draws every segment as past, which is what an index at the step count means.
    val isCompleted = status == OrderStatus._5

    OrderTrackerBar(
        currentStep = if (isCompleted) TotalSteps else currentStep,
        stepCounterLabel = stringResource(
            R.string.tracker_step_counter,
            if (isCompleted) TotalSteps else currentStep + 1,
            TotalSteps,
        ),
        modifier = modifier,
        totalSteps = TotalSteps,
        cancelled = status == OrderStatus._6,
        cancelledLabel = stringResource(R.string.status_cancelled),
    )
}

private const val TotalSteps = 5
