import CleansiaCustomerApi
import Foundation

extension Code {
    func toOrderStatus() -> OrderStatus? {
        guard let value else { return nil }
        return OrderStatus(rawValue: value)
    }
}

extension OrderStatusTrackDto {
    var statusEnum: OrderStatus? {
        status?.toOrderStatus()
    }
}

enum OrderStatusGroup {
    static func isActive(_ status: OrderStatus?) -> Bool {
        switch status {
        case ._2, ._3, ._4: true
        default: false
        }
    }

    static func isUpcoming(_ status: OrderStatus?) -> Bool {
        guard let status else { return false }
        return status != ._5 && status != ._6
    }

    static func isCancellable(_ status: OrderStatus?) -> Bool {
        switch status {
        case ._0, ._1, ._2: true
        default: false
        }
    }

    /// Whether "Report issue" is offered for this order (`canReportIssue` in
    /// `OrderDetailScreen.kt`): Confirmed → Completed. New/Pending have
    /// no cleaner assigned yet so there is nothing to dispute, and a Cancelled
    /// cleaning never happened.
    static func isReportable(_ status: OrderStatus?) -> Bool {
        switch status {
        case ._2, ._3, ._4, ._5: true
        default: false
        }
    }

    static func isCompleted(_ status: OrderStatus?) -> Bool {
        status == ._5
    }

    static func isCancelled(_ status: OrderStatus?) -> Bool {
        status == ._6
    }

    /// The Live Activity wire status (`CleanOrderAttributes.ContentState.status`) an order status opens a
    /// card with, or nil where it carries no card at all. Mirrors the backend's
    /// `LiveActivityEventKeys.ForStatus`: only the service window gets one. Confirmed can be days out, and
    /// a card there says "your cleaner is heading over" while counting down to an appointment nobody has
    /// set off for — besides burning the ~8h ActivityKit budget before the clean (ADR-0029 D2).
    static func liveActivityStatus(_ status: OrderStatus?) -> String? {
        switch status {
        case ._3: "onTheWay"
        case ._4: "inProgress"
        default: nil
        }
    }
}
