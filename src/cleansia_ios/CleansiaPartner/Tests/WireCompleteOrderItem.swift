import CleansiaPartnerApi

extension OrderItem {
    /// A payload that satisfies the mobile API contract: every member `OrderDetail`'s mapper refuses
    /// is populated. Fixtures start here and vary only what they are about, so no test can assert
    /// against a payload the app would refuse in production.
    static func wireComplete() -> OrderItem {
        var item = OrderItem()
        item.id = "order-1"
        item.displayOrderNumber = "ORD-1"
        item.rooms = 0
        item.bathrooms = 0
        item.isAssignedToCurrentUser = false
        item.hasAfterPhotos = false
        return item
    }
}
