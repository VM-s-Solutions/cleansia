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
    private(set) var getByIdCallCount = 0

    /// Each lifecycle command appends `(command, orderId)` here — the test
    /// asserts the carried id is the acted-on id and nothing else (O1/O2).
    private(set) var commands: [(name: String, orderId: String)] = []

    /// When set, the next command suspends until `resumeCommand()` so a test can
    /// hold one mutation mid-flight and fire a second (re-entry guard).
    var suspendCommands = false
    private var commandGate: CheckedContinuation<Void, Never>?

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
        getByIdCallCount += 1
        return byIdResult
    }

    func resumeCommand() {
        commandGate?.resume()
        commandGate = nil
    }

    private func record(_ name: String, _ orderId: String) async -> ApiResult<Void> {
        commands.append((name, orderId))
        if suspendCommands {
            await withCheckedContinuation { commandGate = $0 }
        }
        return commandResult
    }

    func takeOrder(orderId: String) async -> ApiResult<Void> {
        await record("take", orderId)
    }

    func notifyOnTheWay(orderId: String) async -> ApiResult<Void> {
        await record("notifyOnTheWay", orderId)
    }

    func startOrder(orderId: String) async -> ApiResult<Void> {
        await record("start", orderId)
    }

    func completeOrder(orderId: String, actualMinutes _: Int?, notes _: String?) async -> ApiResult<Void> {
        await record("complete", orderId)
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
