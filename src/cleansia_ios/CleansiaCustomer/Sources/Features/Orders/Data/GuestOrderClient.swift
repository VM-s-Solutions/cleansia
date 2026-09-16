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

struct GuestOrderCredentialsRequest: Encodable {
    let displayOrderNumber: String
    let email: String
    let confirmationCode: String

    init(_ key: GuestOrderKey) {
        displayOrderNumber = key.number
        email = key.email
        confirmationCode = key.code
    }
}

struct CancelGuestOrderRequest: Encodable {
    let displayOrderNumber: String
    let email: String
    let confirmationCode: String
    let reason: String?
    let language: String

    init(_ key: GuestOrderKey, reason: String?, language: String) {
        displayOrderNumber = key.number
        email = key.email
        confirmationCode = key.code
        self.reason = reason
        self.language = language
    }
}

struct GuestOrderLookupResponse: Decodable {
    struct CodeWire: Decodable {
        let value: Int?
    }

    struct CurrencyWire: Decodable {
        let code: String?
    }

    let id: String?
    let displayOrderNumber: String?
    let cleaningDateTime: Date?
    let totalPrice: Double?
    let orderStatus: CodeWire?
    let currency: CurrencyWire?
}

struct GuestCancellationPreviewResponse: Decodable {
    let orderId: String?
    let tier: Int?
    let feeAmount: Double?
    let refundAmount: Double?
    let currencyCode: String?
    let expressWaiverForfeitedOnCancel: Bool?
}

struct CancelGuestOrderResponse: Decodable {
    let orderId: String?
    let refundAmount: Double?
    let refundInitiated: Bool?
    let actualRefundAmount: Double?
}

extension GuestOrder {
    init(_ response: GuestOrderLookupResponse) throws {
        id = try response.id.requireNonBlank("id")
        displayOrderNumber = try response.displayOrderNumber.requireNonBlank("displayOrderNumber")
        cleaningDateTime = response.cleaningDateTime
        totalPrice = try response.totalPrice.require("totalPrice")
        currencyCode = try (response.currency?.code).requireNonBlank("currency.code")
        statusValue = try (response.orderStatus?.value).require("orderStatus.value")
    }
}

extension CancellationTier {
    init?(value: Int?) {
        switch value {
        case 0: self = .freeNotAccepted
        case 1: self = .freeOopsWindow
        case 2: self = .freeOutsideWindow
        case 3: self = .partial
        case 4: self = .lastMinute
        default: return nil
        }
    }
}

extension GuestCancellationQuote {
    /// Refused field by field for the same reason the signed-in quote is — see `CancellationQuote`.
    init(_ response: GuestCancellationPreviewResponse) throws {
        orderId = try response.orderId.requireNonBlank("orderId")
        let tier = try CancellationTier(value: response.tier).require("tier")
        let feeAmount = try response.feeAmount.require("feeAmount")
        let refundAmount = try response.refundAmount.require("refundAmount")
        let forfeitsExpressWaiver = try response.expressWaiverForfeitedOnCancel
            .require("expressWaiverForfeitedOnCancel")
        quote = CancellationQuote(
            tier: tier,
            feeAmount: feeAmount,
            refundAmount: refundAmount,
            currencyCode: response.currencyCode,
            forfeitsExpressWaiver: forfeitsExpressWaiver
        )
    }
}

extension GuestOrderCancellation {
    init(_ response: CancelGuestOrderResponse) throws {
        refundAmount = try response.refundAmount.require("refundAmount")
        refundInitiated = try response.refundInitiated.require("refundInitiated")
        actualRefundAmount = response.actualRefundAmount
    }
}

/// Hand-written over the Core spine's anonymous post rather than the generated client: the two cancel
/// routes post-date the last client generation, and the secret must travel in a body, never a URL.
struct LiveGuestOrderClient: GuestOrderClient {
    let transport: AnonymousPosting

    func lookup(_ key: GuestOrderKey) async -> ApiResult<GuestOrder> {
        await apiResult {
            let response: GuestOrderLookupResponse = try await transport.postAnonymous(
                path: "api/Order/Lookup",
                body: GuestOrderCredentialsRequest(key)
            ).get()
            return try GuestOrder(response)
        }
    }

    func cancellationQuote(_ key: GuestOrderKey) async -> ApiResult<GuestCancellationQuote> {
        await apiResult {
            let response: GuestCancellationPreviewResponse = try await transport.postAnonymous(
                path: "api/Order/GuestCancellationPreview",
                body: GuestOrderCredentialsRequest(key)
            ).get()
            return try GuestCancellationQuote(response)
        }
    }

    func cancel(_ key: GuestOrderKey, reason: String?, language: String) async -> ApiResult<GuestOrderCancellation> {
        await apiResult {
            let response: CancelGuestOrderResponse = try await transport.postAnonymous(
                path: "api/Order/CancelGuest",
                body: CancelGuestOrderRequest(key, reason: reason, language: language)
            ).get()
            return try GuestOrderCancellation(response)
        }
    }
}
