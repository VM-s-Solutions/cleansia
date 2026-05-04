package cz.cleansia.customer.features.orders

/**
 * Local presentation enums kept alongside the Orders feature. These mirror the
 * backend `OrderStatus` / `PaymentStatus` enum names but are not auto-mapped —
 * screens that need to branch on status either:
 *
 *  1) work with the backend [cz.cleansia.customer.core.user.CodeDto] directly
 *     via `orderStatus.value` (preferred — the wire is source of truth), or
 *  2) map wire → these local enums where a `when` over named variants is
 *     easier to read (e.g. the legacy mock `OrderDetailScreen`).
 *
 * Values match the backend integer positions:
 *   New=0, Pending=1, Confirmed=2, InProgress=3, Completed=4, Cancelled=5.
 */
enum class OrderStatus { New, Pending, Confirmed, InProgress, Completed, Cancelled }

enum class PaymentStatus { Pending, Paid, Failed, Refunded, Disputed }

/** Map a backend `CodeDto.value` to the local enum. Null / unknown → null. */
fun orderStatusFromValue(value: Int?): OrderStatus? = when (value) {
    0 -> OrderStatus.New
    1 -> OrderStatus.Pending
    2 -> OrderStatus.Confirmed
    3 -> OrderStatus.InProgress
    4 -> OrderStatus.Completed
    5 -> OrderStatus.Cancelled
    else -> null
}
