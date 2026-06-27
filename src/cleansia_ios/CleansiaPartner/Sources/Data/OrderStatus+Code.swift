import CleansiaPartnerApi
import Foundation

/// The generated read DTOs (`OrderItem`/`OrderListItem`/`OrderStatusTrackDto`)
/// carry the status as a `Code` envelope `{type, name, value: Int?}`, NOT the
/// `OrderStatus` enum (which the GetPaged *filter* takes directly). This is the
/// one sanctioned `Code → OrderStatus` mapping (§7.9 Code-convention); read it
/// everywhere instead of inspecting `.value` inline.
extension Code {
    func toOrderStatus() -> OrderStatus? {
        guard let value else { return nil }
        return OrderStatus(rawValue: value)
    }
}

extension OrderListItem {
    var status: OrderStatus? {
        orderStatus?.toOrderStatus()
    }
}

extension OrderItem {
    var status: OrderStatus? {
        orderStatus?.toOrderStatus()
    }
}
