package cz.cleansia.customer.features.orders

import androidx.annotation.StringRes
import cz.cleansia.customer.R

/**
 * Local presentation enums for the Orders feature. Mirrors backend
 * `Cleansia.Core.Domain.Enums.OrderStatus` integer values:
 *   New=0, Pending=1, Confirmed=2, OnTheWay=3, InProgress=4, Completed=5, Cancelled=6.
 */
enum class OrderStatus { New, Pending, Confirmed, OnTheWay, InProgress, Completed, Cancelled }

enum class PaymentStatus { Pending, Paid, Failed, Refunded, Disputed }

fun orderStatusFromValue(value: Int?): OrderStatus? = when (value) {
    0 -> OrderStatus.New
    1 -> OrderStatus.Pending
    2 -> OrderStatus.Confirmed
    3 -> OrderStatus.OnTheWay
    4 -> OrderStatus.InProgress
    5 -> OrderStatus.Completed
    6 -> OrderStatus.Cancelled
    else -> null
}

/**
 * Localized label key for an order status value. Keep in sync with the wire
 * enum above. Returns null for unknown values so the caller can fall back to
 * the raw `name` string from the CodeDto without a phantom translation.
 */
@StringRes
fun orderStatusLabelRes(value: Int?): Int? = when (value) {
    0 -> R.string.orders_status_new
    1 -> R.string.orders_status_pending
    2 -> R.string.orders_status_confirmed
    3 -> R.string.orders_status_on_the_way
    4 -> R.string.orders_status_in_progress
    5 -> R.string.orders_status_completed
    6 -> R.string.orders_status_cancelled
    else -> null
}
