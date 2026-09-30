import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// What the customer owes on one of their orders and has not settled. While any is open, new cash
/// bookings are refused; card bookings are not.
struct Receivable: Equatable, Identifiable {
    let id: String
    let orderId: String
    let displayOrderNumber: String
    let kind: Kind
    let amount: Double
    let currencyCode: String
    let createdOn: Date

    /// The backend's `ReceivableKind`; a value it adds later reads as `.other`.
    enum Kind: Equatable {
        case cancellationFee
        case lockout
        case unpaidCash
        case topUp
        case other

        init(wireValue: Int) {
            switch wireValue {
            case 1: self = .cancellationFee
            case 2: self = .lockout
            case 3: self = .unpaidCash
            case 4: self = .topUp
            default: self = .other
            }
        }
    }
}

protocol ReceivableClient {
    func myReceivables() async -> ApiResult<[Receivable]>
    /// The Stripe Checkout page the customer pays the receivable on.
    func payLink(receivableId: String) async -> ApiResult<URL>
}

struct LiveReceivableClient: ReceivableClient {
    /// Refuses the page rather than dropping a row: a debt missing from this list reads as "nothing to
    /// pay" while the same debt keeps refusing the customer's cash bookings.
    func myReceivables() async -> ApiResult<[Receivable]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerReceivableAPI.receivableGetMine().map { try Receivable($0) }
        }
    }

    func payLink(receivableId: String) async -> ApiResult<URL> {
        await apiResult(mapError: ApiError.fromGenerated) {
            let link = try await CustomerReceivableAPI.receivableCreatePayLink(id: receivableId)
            let checkoutUrl = try link.checkoutUrl.requireNonBlank("checkoutUrl")
            return try URL(string: checkoutUrl).require("checkoutUrl")
        }
    }
}

extension Receivable {
    init(_ dto: MyReceivableDto) throws {
        try self.init(
            id: dto.id.requireNonBlank("id"),
            orderId: dto.orderId.requireNonBlank("orderId"),
            displayOrderNumber: dto.displayOrderNumber.requireNonBlank("displayOrderNumber"),
            kind: Kind(wireValue: dto.kind.require("kind").value.require("kind.value")),
            amount: dto.amount.require("amount"),
            currencyCode: dto.currencyCode.requireNonBlank("currencyCode"),
            createdOn: dto.createdOn.require("createdOn")
        )
    }
}
