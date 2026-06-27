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

    /// Note/issue mutations appended here for the notes-section tests.
    private(set) var noteCommands: [(name: String, id: String?, content: String?)] = []

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
        return await gated()
    }

    private func recordNote(_ name: String, id: String?, content: String?) async -> ApiResult<Void> {
        noteCommands.append((name: name, id: id, content: content))
        return await gated()
    }

    private func gated() async -> ApiResult<Void> {
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

    func addNote(orderId _: String, content: String) async -> ApiResult<Void> {
        await recordNote("addNote", id: nil, content: content)
    }

    func updateNote(orderId _: String, noteId: String, content: String) async -> ApiResult<Void> {
        await recordNote("updateNote", id: noteId, content: content)
    }

    func deleteNote(orderId _: String, noteId: String) async -> ApiResult<Void> {
        await recordNote("deleteNote", id: noteId, content: nil)
    }

    func reportIssue(orderId _: String, description: String) async -> ApiResult<Void> {
        await recordNote("reportIssue", id: nil, content: description)
    }

    func updateIssue(orderId _: String, issueId: String, description: String) async -> ApiResult<Void> {
        await recordNote("updateIssue", id: issueId, content: description)
    }

    func deleteIssue(orderId _: String, issueId: String) async -> ApiResult<Void> {
        await recordNote("deleteIssue", id: issueId, content: nil)
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
