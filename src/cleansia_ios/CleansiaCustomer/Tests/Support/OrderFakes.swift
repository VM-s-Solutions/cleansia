import CleansiaCore
import CleansiaCustomerApi
import Foundation
@testable import CleansiaCustomer

final class FakeOrderClient: OrderClient, @unchecked Sendable {
    var pages: [OrdersPage] = []
    var pageError: ApiError?
    private(set) var pageRequests: [(offset: Int, limit: Int)] = []

    var detailResults: [ApiResult<OrderItem>] = []
    private(set) var detailCallCount = 0

    var cancelResult: ApiResult<CancelOrderResponse> = .success(CancelOrderResponse())
    private(set) var cancelCallCount = 0
    private(set) var lastCancelReason: String??

    var reviewResult: ApiResult<OrderReviewDto> = .success(OrderReviewDto())
    private(set) var reviewCallCount = 0
    private(set) var lastReview: (rating: Int, comment: String?)?

    var receiptResult: ApiResult<URL> = .success(URL(fileURLWithPath: "/tmp/receipt.pdf"))
    private(set) var receiptCallCount = 0

    var photosResults: [ApiResult<GetOrderPhotosResponse>] = []
    private(set) var photosCallCount = 0

    func getMyOrders(offset: Int, limit: Int) async -> ApiResult<OrdersPage> {
        pageRequests.append((offset, limit))
        if let pageError { return .failure(pageError) }
        let index = min(pageRequests.count - 1, pages.count - 1)
        guard index >= 0 else { return .success(OrdersPage(items: [], total: 0)) }
        return .success(pages[index])
    }

    func getById(orderId _: String) async -> ApiResult<OrderItem> {
        defer { detailCallCount += 1 }
        let index = min(detailCallCount, detailResults.count - 1)
        guard index >= 0 else { return .failure(ApiError(httpStatus: 500)) }
        return detailResults[index]
    }

    func cancel(orderId _: String, reason: String?) async -> ApiResult<CancelOrderResponse> {
        cancelCallCount += 1
        lastCancelReason = .some(reason)
        return cancelResult
    }

    func submitReview(orderId _: String, rating: Int, comment: String?) async -> ApiResult<OrderReviewDto> {
        reviewCallCount += 1
        lastReview = (rating, comment)
        return reviewResult
    }

    func downloadReceipt(orderId _: String) async -> ApiResult<URL> {
        receiptCallCount += 1
        return receiptResult
    }

    func getPhotos(orderId _: String) async -> ApiResult<GetOrderPhotosResponse> {
        defer { photosCallCount += 1 }
        let index = min(photosCallCount, photosResults.count - 1)
        guard index >= 0 else { return .failure(ApiError(httpStatus: 500)) }
        return photosResults[index]
    }
}

enum OrderFixtures {
    static func listItem(id: String, statusValue: Int) -> OrderListItem {
        OrderListItem(
            id: id,
            orderStatus: Code(type: "OrderStatus", name: nil, value: statusValue)
        )
    }

    static func detail(id: String = "o1", statusValue: Int) -> OrderItem {
        OrderItem(
            id: id,
            orderStatus: Code(type: "OrderStatus", name: nil, value: statusValue)
        )
    }

    static func track(statusValue: Int, createdOn: Date) -> OrderStatusTrackDto {
        OrderStatusTrackDto(
            status: Code(type: "OrderStatus", name: nil, value: statusValue),
            createdOn: createdOn
        )
    }
}
