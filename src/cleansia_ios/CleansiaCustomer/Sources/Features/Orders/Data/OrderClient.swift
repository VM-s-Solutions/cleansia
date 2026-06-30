import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct OrdersPage: Equatable {
    let items: [OrderListItem]
    let total: Int
}

protocol OrderClient: Sendable {
    func getMyOrders(offset: Int, limit: Int) async -> ApiResult<OrdersPage>
    func getById(orderId: String) async -> ApiResult<OrderItem>
    func cancel(orderId: String, reason: String?) async -> ApiResult<CancelOrderResponse>
    func submitReview(orderId: String, rating: Int, comment: String?) async -> ApiResult<OrderReviewDto>
    func downloadReceipt(orderId: String) async -> ApiResult<URL>
    func getPhotos(orderId: String) async -> ApiResult<GetOrderPhotosResponse>
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
}
