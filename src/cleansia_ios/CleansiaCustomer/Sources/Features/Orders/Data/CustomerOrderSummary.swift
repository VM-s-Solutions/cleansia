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

/// One row of the customer's orders list, shared by the Orders tab and the Home recent/order-again
/// rows — one repository, one row model, so the two cannot disagree about the same booking.
///
/// **Refuse the row's own money; drop the row that has no id.** The price on a card is *that order's*
/// total, not a share of a figure computed elsewhere, so there is no page-level rollup to protect and
/// no separate page question to answer: a coerced `0` says this cleaning cost nothing, on the row it
/// belongs to. Because the row is an element of the page, refusing it refuses the page — an order is
/// priced as the server priced it or the list says it could not be loaded.
///
/// Identity goes the other way and it is the same ruling read from the other end: an id-less row was
/// already dead, since every card navigates by id, and nothing on either surface sums or counts these
/// rows against a figure — the paged `total` is the server's own count and the filter chips count the
/// rows actually shown — so dropping one falsifies nothing, while refusing the page would hide every
/// order the server answered correctly.
///
/// `estimatedTime` is refused for the reason spelled out on ``CustomerOrderDetail``: the wire declares
/// it non-nullable and the detail's Live Activity turns an absent one into a one-minute cleaning, so
/// the two surfaces answer it the same way rather than each guessing.
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
    }
}
