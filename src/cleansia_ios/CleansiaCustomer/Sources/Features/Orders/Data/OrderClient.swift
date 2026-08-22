import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct OrdersPage: Equatable {
    let items: [CustomerOrderSummary]
    let total: Int
}

/// **Refuse.** Both members are non-nullable on the server and the pair is a statement about the
/// customer's money: coerced, `refundInitiated` reads `false` and the screen says "cancelled, no
/// refund" over a refund the server did start. There is no page and no row here — one response.
struct OrderCancellation: Equatable {
    let refundAmount: Double
    let refundInitiated: Bool

    init(refundAmount: Double, refundInitiated: Bool) {
        self.refundAmount = refundAmount
        self.refundInitiated = refundInitiated
    }

    init(_ response: CancelOrderResponse) throws {
        refundAmount = try response.refundAmount.require("refundAmount")
        refundInitiated = try response.refundInitiated.require("refundInitiated")
    }

    var refunded: Double? {
        guard refundInitiated, refundAmount > 0 else { return nil }
        return refundAmount
    }
}

/// Result of `orderConfirmRecurring`. A nil/empty `clientSecret` means the
/// backend already confirmed the order (cash path); a non-empty one means the
/// card path needs a PaymentSheet to finish.
struct RecurringConfirmation: Equatable {
    let clientSecret: String?
    let stripeCustomerId: String?
    let ephemeralKey: String?

    var needsPayment: Bool {
        !(clientSecret?.isEmpty ?? true)
    }
}

protocol OrderClient: Sendable {
    func getMyOrders(offset: Int, limit: Int) async -> ApiResult<OrdersPage>
    func getById(orderId: String) async -> ApiResult<CustomerOrderDetail>
    func cancel(orderId: String, reason: String?) async -> ApiResult<OrderCancellation>
    func submitReview(
        orderId: String,
        rating: Int,
        comment: String?,
        tags: [CustomerReviewTag]
    ) async -> ApiResult<OrderReviewDto>
    func downloadReceipt(orderId: String) async -> ApiResult<URL>
    func getPhotos(orderId: String) async -> ApiResult<OrderPhotos>
    func confirmRecurring(orderId: String) async -> ApiResult<RecurringConfirmation>
    func cancellationQuote(orderId: String) async -> ApiResult<CancellationQuote>
}

struct LiveOrderClient: OrderClient {
    /// **Refuse the count, keep the rows.** `total` is the server's own record count and the ONLY
    /// input to `hasMore` (`orders.count < total`), so a coerced `0` reports every page as the last
    /// one and the customer's older orders stop existing rather than fail to load. The rows
    /// themselves default to empty: an absent page and an empty one are the same fact to the list,
    /// which counts what it shows and sums nothing. Each surviving row makes its own two rulings —
    /// see ``CustomerOrderSummary``.
    func getMyOrders(offset: Int, limit: Int) async -> ApiResult<OrdersPage> {
        await apiResult(mapError: ApiError.fromGenerated) {
            let paged = try await CustomerOrderAPI.orderGetMyOrders(offset: offset, limit: limit)
            return try OrdersPage(
                items: (paged.data ?? []).compactMap(CustomerOrderSummary.init),
                total: paged.total.require("total")
            )
        }
    }

    func getById(orderId: String) async -> ApiResult<CustomerOrderDetail> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerOrderDetail(CustomerOrderAPI.orderGetById(orderId: orderId))
        }
    }

    func cancel(orderId: String, reason: String?) async -> ApiResult<OrderCancellation> {
        let command = CancelOrderCommand(orderId: orderId, reason: reason)
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await OrderCancellation(CustomerOrderAPI.orderCancelOrder(cancelOrderCommand: command))
        }
    }

    func submitReview(
        orderId: String,
        rating: Int,
        comment: String?,
        tags: [CustomerReviewTag]
    ) async -> ApiResult<OrderReviewDto> {
        // The app-side raw values ARE the wire values, so this is the identity — no lookup table to
        // drift from the server's enum.
        let command = SubmitOrderReviewCommand(
            orderId: orderId,
            rating: rating,
            comment: comment,
            tags: tags.compactMap { ReviewTag(rawValue: $0.rawValue) }
        )
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerOrderAPI.orderSubmitReview(submitOrderReviewCommand: command)
        }
    }

    func downloadReceipt(orderId: String) async -> ApiResult<URL> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerOrderAPI.orderDownloadReceipt(orderId: orderId)
        }
    }

    func getPhotos(orderId: String) async -> ApiResult<OrderPhotos> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await OrderPhotos(CustomerOrderAPI.orderGetPhotos(orderId: orderId))
        }
    }

    func confirmRecurring(orderId: String) async -> ApiResult<RecurringConfirmation> {
        let command = ConfirmRecurringOrderCommand(orderId: orderId)
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerOrderAPI.orderConfirmRecurring(confirmRecurringOrderCommand: command)
        }
        return result.map {
            RecurringConfirmation(
                clientSecret: $0.clientSecret,
                stripeCustomerId: $0.stripeCustomerId,
                ephemeralKey: $0.ephemeralKey
            )
        }
    }

    func cancellationQuote(orderId: String) async -> ApiResult<CancellationQuote> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CancellationQuote(CustomerOrderAPI.orderCancellationPreview(orderId: orderId))
        }
    }
}
