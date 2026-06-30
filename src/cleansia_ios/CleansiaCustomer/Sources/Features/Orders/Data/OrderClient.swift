import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct OrdersPage: Equatable {
    let items: [OrderListItem]
    let total: Int
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
    func getById(orderId: String) async -> ApiResult<OrderItem>
    func cancel(orderId: String, reason: String?) async -> ApiResult<CancelOrderResponse>
    func submitReview(orderId: String, rating: Int, comment: String?) async -> ApiResult<OrderReviewDto>
    func downloadReceipt(orderId: String) async -> ApiResult<URL>
    func getPhotos(orderId: String) async -> ApiResult<GetOrderPhotosResponse>
    func confirmRecurring(orderId: String) async -> ApiResult<RecurringConfirmation>
}

struct LiveOrderClient: OrderClient {
    func getMyOrders(offset: Int, limit: Int) async -> ApiResult<OrdersPage> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerOrderAPI.orderGetMyOrders(offset: offset, limit: limit)
        }
        return result.map { paged in
            OrdersPage(items: paged.data ?? [], total: paged.total ?? 0)
        }
    }

    func getById(orderId: String) async -> ApiResult<OrderItem> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerOrderAPI.orderGetById(orderId: orderId)
        }
    }

    func cancel(orderId: String, reason: String?) async -> ApiResult<CancelOrderResponse> {
        let command = CancelOrderCommand(orderId: orderId, reason: reason)
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerOrderAPI.orderCancelOrder(cancelOrderCommand: command)
        }
    }

    func submitReview(orderId: String, rating: Int, comment: String?) async -> ApiResult<OrderReviewDto> {
        let command = SubmitOrderReviewCommand(orderId: orderId, rating: rating, comment: comment)
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerOrderAPI.orderSubmitReview(submitOrderReviewCommand: command)
        }
    }

    func downloadReceipt(orderId: String) async -> ApiResult<URL> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerOrderAPI.orderDownloadReceipt(orderId: orderId)
        }
    }

    func getPhotos(orderId: String) async -> ApiResult<GetOrderPhotosResponse> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerOrderAPI.orderGetPhotos(orderId: orderId)
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
}
