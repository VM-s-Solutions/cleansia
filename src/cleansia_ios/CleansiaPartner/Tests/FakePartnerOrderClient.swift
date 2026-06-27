import CleansiaCore
import CleansiaPartnerApi
import Foundation
@testable import CleansiaPartner

@MainActor
final class FakePartnerOrderClient: PartnerOrderClient {
    var employeeIdResult: ApiResult<String> = .success("emp-self")
    var pagedResult: ApiResult<[OrderListItem]> = .success([])
    var byIdResult: ApiResult<OrderItem> = .success(OrderItem())
    var commandResult: ApiResult<Void> = .success(())

    private(set) var queries: [OrderPageQuery] = []
    private(set) var employeeIdCallCount = 0
    private(set) var getPagedCallCount = 0

    func currentEmployeeId() async -> ApiResult<String> {
        employeeIdCallCount += 1
        return employeeIdResult
    }

    func getPaged(_ query: OrderPageQuery) async -> ApiResult<[OrderListItem]> {
        getPagedCallCount += 1
        queries.append(query)
        return pagedResult
    }

    func getById(orderId _: String) async -> ApiResult<OrderItem> {
        byIdResult
    }

    func takeOrder(orderId _: String) async -> ApiResult<Void> {
        commandResult
    }

    func notifyOnTheWay(orderId _: String) async -> ApiResult<Void> {
        commandResult
    }

    func startOrder(orderId _: String) async -> ApiResult<Void> {
        commandResult
    }

    func completeOrder(orderId _: String, actualMinutes _: Int?, notes _: String?) async -> ApiResult<Void> {
        commandResult
    }

    func addNote(orderId _: String, content _: String) async -> ApiResult<Void> {
        commandResult
    }

    func updateNote(orderId _: String, noteId _: String, content _: String) async -> ApiResult<Void> {
        commandResult
    }

    func deleteNote(orderId _: String, noteId _: String) async -> ApiResult<Void> {
        commandResult
    }

    func reportIssue(orderId _: String, description _: String) async -> ApiResult<Void> {
        commandResult
    }

    func updateIssue(orderId _: String, issueId _: String, description _: String) async -> ApiResult<Void> {
        commandResult
    }

    func deleteIssue(orderId _: String, issueId _: String) async -> ApiResult<Void> {
        commandResult
    }
}

extension OrderListItem {
    static func sample(
        id: String,
        status: OrderStatus = ._2,
        pay: Double = 500,
        cleaningDateTime: Date? = nil,
        customerName: String? = nil,
        customerAddress: String? = nil,
        displayOrderNumber: String? = nil,
        latitude: Double? = nil,
        longitude: Double? = nil
    ) -> OrderListItem {
        var item = OrderListItem()
        item.id = id
        item.orderStatus = Code(value: status.rawValue)
        item.estimatedCleanerPay = pay
        item.cleaningDateTime = cleaningDateTime
        item.customerName = customerName
        item.customerAddress = customerAddress
        item.displayOrderNumber = displayOrderNumber
        item.customerAddressLatitude = latitude
        item.customerAddressLongitude = longitude
        return item
    }
}
