import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// The three things a guest booking is keyed by. Trimmed on entry and refused blank, so the server is
/// never asked about a booking nobody named.
struct GuestOrderKey: Equatable {
    let number: String
    let email: String
    let code: String

    init?(number: String, email: String, code: String) {
        let trimmed = [number, email, code].map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
        guard trimmed.allSatisfy({ !$0.isEmpty }) else { return nil }
        self.number = trimmed[0]
        self.email = trimmed[1]
        self.code = trimmed[2]
    }
}

struct GuestOrder: Equatable {
    let id: String
    let displayOrderNumber: String
    let cleaningDateTime: Date?
    let totalPrice: Double
    let currencyCode: String
    let statusValue: Int

    var status: OrderStatus? {
        OrderStatus(rawValue: statusValue)
    }

    /// New, Pending, Confirmed and OnTheWay — everything the server's `CancellationAssessor` does not
    /// block. Same set the web track-order page and Android's guest screen offer the button on.
    var isCancellable: Bool {
        switch status {
        case ._0, ._1, ._2, ._3: true
        default: false
        }
    }
}

/// The quote comes back stamped with the order it was priced for, and the screen refuses one for any
/// other order: the sheet is opened on a looked-up booking, and a fee for a different one is worse than
/// no fee.
struct GuestCancellationQuote: Equatable {
    let orderId: String
    let quote: CancellationQuote
}

/// `actualRefundAmount` is the money actually sent back; `refundAmount` is the policy figure. A guest's
/// receipt is the one screen they will keep, so it quotes the former and never borrows the latter.
struct GuestOrderCancellation: Equatable {
    let refundAmount: Double
    let refundInitiated: Bool
    let actualRefundAmount: Double?

    var refunded: Double? {
        guard refundInitiated, let amount = actualRefundAmount, amount > 0 else { return nil }
        return amount
    }
}

protocol GuestOrderClient: Sendable {
    func lookup(_ key: GuestOrderKey) async -> ApiResult<GuestOrder>
    func cancellationQuote(_ key: GuestOrderKey) async -> ApiResult<GuestCancellationQuote>
    func cancel(_ key: GuestOrderKey, reason: String?, language: String) async -> ApiResult<GuestOrderCancellation>
}

extension GuestOrder {
    init(_ response: LookupOrderResponse) throws {
        id = try response.id.requireNonBlank("id")
        displayOrderNumber = try response.displayOrderNumber.requireNonBlank("displayOrderNumber")
        cleaningDateTime = response.cleaningDateTime
        totalPrice = try response.totalPrice.require("totalPrice")
        currencyCode = try (response.currency?.code).requireNonBlank("currency.code")
        statusValue = try (response.orderStatus?.value).require("orderStatus.value")
    }
}

extension GuestCancellationQuote {
    /// The figures are refused field by field for the same reason the signed-in quote's are — see
    /// `CancellationQuote`; the order id is refused with them because it is what pins the quote to the
    /// booking on screen.
    init(_ response: GetCancellationFeePreviewResponse) throws {
        orderId = try response.orderId.requireNonBlank("orderId")
        quote = try CancellationQuote(response)
    }
}

extension GuestOrderCancellation {
    init(_ response: CancelOrderResponse) throws {
        refundAmount = try response.refundAmount.require("refundAmount")
        refundInitiated = try response.refundInitiated.require("refundInitiated")
        actualRefundAmount = response.actualRefundAmount
    }
}

struct LiveGuestOrderClient: GuestOrderClient {
    func lookup(_ key: GuestOrderKey) async -> ApiResult<GuestOrder> {
        let query = LookupOrderQuery(displayOrderNumber: key.number, email: key.email, confirmationCode: key.code)
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await GuestOrder(CustomerOrderAPI.orderLookup(lookupOrderQuery: query))
        }
    }

    func cancellationQuote(_ key: GuestOrderKey) async -> ApiResult<GuestCancellationQuote> {
        let query = GetGuestCancellationFeePreviewQuery(
            displayOrderNumber: key.number,
            email: key.email,
            confirmationCode: key.code
        )
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await GuestCancellationQuote(
                CustomerOrderAPI.orderGuestCancellationPreview(getGuestCancellationFeePreviewQuery: query)
            )
        }
    }

    func cancel(_ key: GuestOrderKey, reason: String?, language: String) async -> ApiResult<GuestOrderCancellation> {
        let command = CancelGuestOrderCommand(
            displayOrderNumber: key.number,
            email: key.email,
            confirmationCode: key.code,
            reason: reason,
            language: language
        )
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await GuestOrderCancellation(CustomerOrderAPI.orderCancelGuest(cancelGuestOrderCommand: command))
        }
    }
}
