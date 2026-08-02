package cz.cleansia.partner.features.orders

import androidx.annotation.StringRes
import androidx.compose.runtime.Composable
import androidx.compose.ui.res.stringResource
import cz.cleansia.partner.BuildConfig
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderStatus

/** Convert a backend `Code` (carries the integer value) to the typed enum. */
fun Code?.toOrderStatus(): OrderStatus? = this?.value?.let { v ->
    OrderStatus.values().firstOrNull { it.value == v }
}

/**
 * Backend status ordinals: 0 New, 1 Pending, 2 Confirmed, 3 OnTheWay,
 * 4 InProgress, 5 Completed, 6 Cancelled. Null means the wire carried an
 * ordinal this build has no enum case for.
 */
@StringRes
fun orderStatusLabelRes(status: OrderStatus?): Int? = when (status) {
    OrderStatus._0 -> R.string.status_new
    OrderStatus._1 -> R.string.status_pending
    OrderStatus._2 -> R.string.status_confirmed
    OrderStatus._3 -> R.string.status_on_the_way
    OrderStatus._4 -> R.string.status_in_progress
    OrderStatus._5 -> R.string.status_completed
    OrderStatus._6 -> R.string.status_cancelled
    null -> null
}

/**
 * Label for a status the cleaner sees. Resolves the ordinal to a translated
 * string; the `Code.name` the backend ships alongside it is a non-localized
 * enum name ("OnTheWay") and must never reach a translated build. Mirrors the
 * iOS `OrderStatusLabel` and the customer app's `orderStatusLabelRes`.
 */
@Composable
fun orderStatusLabel(status: OrderStatus?, wireName: String? = null): String =
    orderStatusLabelRes(status)?.let { stringResource(it) }
        ?: unknownOrderStatusLabel(wireName, BuildConfig.DEBUG)

/**
 * A backend status added after this build shipped. Production shows a
 * placeholder; a debug build shows the prettified wire name so the gap
 * surfaces to us instead of to a cleaner.
 *
 * [isDebug] is a parameter, not a read of `BuildConfig`, so the production
 * branch stays assertable from a debug unit test.
 */
internal fun unknownOrderStatusLabel(wireName: String?, isDebug: Boolean): String {
    if (!isDebug) return UNKNOWN_STATUS
    val raw = wireName?.trim()?.takeIf { it.isNotEmpty() } ?: return UNKNOWN_STATUS
    return raw
        .replace(Regex("([a-z])([A-Z])")) { m -> "${m.groupValues[1]} ${m.groupValues[2].lowercase()}" }
        .replaceFirstChar { it.uppercase() }
}

private const val UNKNOWN_STATUS = "—"
