import CleansiaCore
import Foundation

/// Turns the server's cancellation-reason KEY into a sentence the customer can read.
///
/// **Only the platform's own reasons arrive here.** `OrderItem.SystemCancellationReason` is populated
/// by the backend only when `CancelledBy` is `System`; a human cancellation leaves it null. That
/// gating is deliberate and lives server-side, because the same column also holds an admin's free-text
/// note written for other staff — so this type never has to decide whether a value is safe to render.
///
/// **An unknown key renders NOTHING, and that is the important half.** These strings are minted by a
/// backend that ships independently of the app, so a newer server can name a reason this build has
/// never heard of. Falling back to the raw key would put `order.cancelled.something` on screen; the
/// customer already sees the Cancelled status either way, so silence costs them a sentence and a
/// wrong guess costs them their confidence in the whole screen.
enum CancellationReasonCopy {
    /// Mirrors `Cleansia.Core.Domain.Orders.OrderCancellationReasons`. Both sides are keys rather than
    /// sentences precisely so this mapping can exist.
    private static let copy: [String: String] = [
        "order.cancelled.payment_not_completed": "order_cancelled_reason_payment_not_completed",
        "order.cancelled.recurring_not_confirmed": "order_cancelled_reason_recurring_not_confirmed"
    ]

    static func text(for reason: String?) -> String? {
        guard let reason, !reason.isBlank, let key = copy[reason] else { return nil }
        return L10n.localized(key)
    }
}
