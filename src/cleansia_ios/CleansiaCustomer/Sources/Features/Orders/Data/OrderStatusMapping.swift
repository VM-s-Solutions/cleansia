import CleansiaCustomerApi
import Foundation

extension Code {
    func toOrderStatus() -> OrderStatus? {
        guard let value else { return nil }
        return OrderStatus(rawValue: value)
    }
}

extension OrderItem {
    var status: OrderStatus? {
        orderStatus?.toOrderStatus()
    }
}

extension OrderListItem {
    var status: OrderStatus? {
        orderStatus?.toOrderStatus()
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

    static func isCompleted(_ status: OrderStatus?) -> Bool {
        status == ._5
    }

    static func isCancelled(_ status: OrderStatus?) -> Bool {
        status == ._6
    }
}
