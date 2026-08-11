import CleansiaCustomerApi

extension OrderItem {
    /// A payload that satisfies the mobile API contract: every member `CustomerOrderDetail`'s mapper
    /// refuses is populated. Wire fixtures start here and break exactly one field, so a test can only
    /// ever be about the field it removed.
    static func wireComplete() -> OrderItem {
        var item = OrderItem()
        item.id = "o1"
        item.rooms = 3
        item.bathrooms = 1
        item.estimatedTime = 180
        item.totalPrice = 1590
        item.originalSubtotal = 2100
        return item
    }
}

extension OrderListItem {
    /// The same, for a row of the orders page.
    static func wireComplete() -> OrderListItem {
        var item = OrderListItem()
        item.id = "o1"
        item.estimatedTime = 180
        item.totalPrice = 1590
        return item
    }
}
