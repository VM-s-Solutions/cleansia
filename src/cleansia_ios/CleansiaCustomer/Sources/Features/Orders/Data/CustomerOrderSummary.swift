import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// A catalog line's name as the list and the Home rows print it: the frozen snapshot name plus the
/// translations that rode with the order, resolved at render so a live language switch re-resolves it.
/// Nothing on either surface prices a line, so no line price is carried and none can be fabricated.
struct CustomerOrderLineName: Equatable, Hashable {
    let name: String?
    let translations: [String: Translation]?
}

/// One row of the customer's orders list, shared by the Orders tab and the Home rows — one repository,
/// one row model, so the two cannot disagree about the same booking.
///
/// **Refuse the row's own money; drop the row that has no id.** A coerced `0` says this cleaning cost
/// nothing, on the row it belongs to. → /decisions/adr-0048
struct CustomerOrderSummary: Equatable {
    let id: String
    let displayOrderNumber: String?
    let statusCode: Code?
    let cleaningDateTime: Date?
    let estimatedMinutes: Int
    let address: String?
    let total: Double
    let currencyCode: String?
    let services: [CustomerOrderLineName]
    let packages: [CustomerOrderLineName]
    /// Whether the customer has already reviewed this booking. Server-projected onto the LIST so the
    /// completion prompt can be decided from the warm cache — the alternative is a detail fetch per
    /// candidate every time the app opens.
    let hasReview: Bool

    var status: OrderStatus? {
        statusCode?.toOrderStatus()
    }
}

extension CustomerOrderSummary {
    /// `nil` for a row with no usable id — the drop half of the ruling above. The refusals throw.
    init?(_ item: OrderListItem) throws {
        guard let id = item.id, !id.isBlank else { return nil }
        self.id = id
        displayOrderNumber = item.displayOrderNumber
        statusCode = item.orderStatus
        cleaningDateTime = item.cleaningDateTime
        estimatedMinutes = try item.estimatedTime.require("estimatedTime")
        address = item.customerAddress
        total = try item.totalPrice.require("totalPrice")
        currencyCode = item.currency?.code
        services = (item.selectedServices ?? []).map {
            CustomerOrderLineName(name: $0.name, translations: $0.translations)
        }
        packages = (item.selectedPackages ?? []).map {
            CustomerOrderLineName(name: $0.name, translations: $0.translations)
        }
        // Defaulted rather than required: an older server that omits it yields "not reviewed", which
        // costs at most one redundant prompt. Refusing would drop the whole row off the orders list.
        hasReview = item.hasReview ?? false
    }
}
