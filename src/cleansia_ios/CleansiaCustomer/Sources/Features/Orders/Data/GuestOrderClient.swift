import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// What a guest booking is keyed by: the per-order access token the confirmation e-mail carries. The
/// guest pastes the whole tracking link or the token out of it; either way only the token is sent, and
/// a blank one is refused here so the server is never asked about a booking nobody named.
struct GuestOrderKey: Equatable {
    let accessToken: String

    init?(pasted: String) {
        let token = Self.token(in: pasted)
        guard !token.isEmpty else { return nil }
        accessToken = token
    }

    private static func token(in pasted: String) -> String {
        let trimmed = pasted.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let marker = trimmed.range(of: "[?&]token=", options: .regularExpression) else { return trimmed }
        let rest = trimmed[marker.upperBound...]
        let end = rest.firstIndex { $0 == "&" || $0 == "#" } ?? rest.endIndex
        return String(rest[..<end])
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

    var isCancellable: Bool {
        OrderStatusGroup.isCancellable(status)
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
/// receipt is the one screen they will keep, so it quotes the former and never borrows the latter —
/// the same rule `OrderCancellation` applies to the signed-in confirmation.
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
        let query = LookupOrderQuery(accessToken: key.accessToken)
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await GuestOrder(CustomerOrderAPI.orderLookup(lookupOrderQuery: query))
        }
    }

    func cancellationQuote(_ key: GuestOrderKey) async -> ApiResult<GuestCancellationQuote> {
        let query = GetGuestCancellationFeePreviewQuery(accessToken: key.accessToken)
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await GuestCancellationQuote(
                CustomerOrderAPI.orderGuestCancellationPreview(getGuestCancellationFeePreviewQuery: query)
            )
        }
    }

    func cancel(_ key: GuestOrderKey, reason: String?, language: String) async -> ApiResult<GuestOrderCancellation> {
        let command = CancelGuestOrderCommand(
            accessToken: key.accessToken,
            reason: reason,
            language: language
        )
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await GuestOrderCancellation(CustomerOrderAPI.orderCancelGuest(cancelGuestOrderCommand: command))
        }
    }
}
