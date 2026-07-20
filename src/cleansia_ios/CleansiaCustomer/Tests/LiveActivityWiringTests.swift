import CleansiaCore
import CleansiaCustomerApi
import Foundation
import XCTest
@testable import CleansiaCustomer

private struct StubDeviceIdProvider: DeviceIdProviding {
    let deviceId: String
}

private final class SpyLiveActivityApi: LiveActivityApi, @unchecked Sendable {
    private(set) var registered: [RegisterLiveActivityTokenCommand] = []
    private(set) var unregistered: [(orderId: String, deviceId: String)] = []
    var registerError: Error?
    var unregisterError: Error?

    func register(_ command: RegisterLiveActivityTokenCommand) async throws {
        registered.append(command)
        if let registerError { throw registerError }
    }

    func unregister(orderId: String, deviceId: String) async throws {
        unregistered.append((orderId, deviceId))
        if let unregisterError { throw unregisterError }
    }
}

private final class SpyLiveActivitySync: OrderLiveActivitySyncing, @unchecked Sendable {
    struct Started: Equatable {
        let orderId: String
        let orderNumber: String
        let start: Date
        let end: Date
    }

    private(set) var started: [Started] = []
    private(set) var ended: [String] = []

    func start(orderId: String, orderNumber: String, scheduledStart: Date, scheduledEnd: Date) {
        started.append(Started(orderId: orderId, orderNumber: orderNumber, start: scheduledStart, end: scheduledEnd))
    }

    func end(orderId: String) {
        ended.append(orderId)
    }
}

@MainActor
final class LiveActivityRegistrarTests: XCTestCase {
    private func makeSUT(deviceId: String = "device-1") -> (CustomerLiveActivityRegistrar, SpyLiveActivityApi) {
        let api = SpyLiveActivityApi()
        let sut = CustomerLiveActivityRegistrar(deviceIdProvider: StubDeviceIdProvider(deviceId: deviceId), api: api)
        return (sut, api)
    }

    func testRegisterMapsDeviceTokenAndOrderId() async throws {
        let (sut, api) = makeSUT()

        await sut.register(orderId: "order-9", orderNumber: "1042", token: "abc123")

        let command = try XCTUnwrap(api.registered.first)
        XCTAssertEqual(api.registered.count, 1)
        XCTAssertEqual(command.deviceId, "device-1")
        XCTAssertEqual(command.token, "abc123")
        XCTAssertEqual(command.orderId, "order-9")
    }

    func testRegisterPushToStartSendsNilOrderId() async throws {
        let (sut, api) = makeSUT()

        await sut.registerPushToStart(token: "start-token")

        let command = try XCTUnwrap(api.registered.first)
        XCTAssertNil(command.orderId)
        XCTAssertEqual(command.token, "start-token")
        XCTAssertEqual(command.deviceId, "device-1")
    }

    func testDeregisterSendsOrderIdAndDeviceId() async throws {
        let (sut, api) = makeSUT()

        await sut.deregister(orderId: "order-9")

        let call = try XCTUnwrap(api.unregistered.first)
        XCTAssertEqual(call.orderId, "order-9")
        XCTAssertEqual(call.deviceId, "device-1")
    }

    func testFailedRegistrationIsSwallowed() async {
        let (sut, api) = makeSUT()
        api.registerError = ApiError(httpStatus: 409)

        await sut.register(orderId: "o", orderNumber: "n", token: "t")

        XCTAssertEqual(api.registered.count, 1)
    }
}

@MainActor
final class OrderLiveActivitySyncTests: XCTestCase {
    private let start = Date(timeIntervalSince1970: 1_700_000_000)

    private func order(statusValue: Int) -> OrderItem {
        OrderItem(
            id: "o1",
            displayOrderNumber: "1042",
            cleaningDateTime: start,
            estimatedTime: 90,
            orderStatus: Code(type: "OrderStatus", name: nil, value: statusValue)
        )
    }

    private func makeVM(_ client: FakeOrderClient, sync: SpyLiveActivitySync) -> OrderDetailViewModel {
        OrderDetailViewModel(
            orderId: "o1",
            client: client,
            repository: OrderRepository(client: client),
            snackbar: SnackbarController(),
            eventBus: OrderEventBus(),
            liveActivity: sync,
            pollInterval: 3600
        )
    }

    func testActiveOrderStartsWithTheAppointmentWindow() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 3))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertEqual(sync.started.count, 1)
        XCTAssertEqual(sync.started.first?.orderId, "o1")
        XCTAssertEqual(sync.started.first?.orderNumber, "1042")
        XCTAssertEqual(sync.started.first?.start, start)
        XCTAssertEqual(sync.started.first?.end, start.addingTimeInterval(90 * 60))
        XCTAssertTrue(sync.ended.isEmpty)
    }

    func testCompletedOrderEnds() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 5))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertEqual(sync.ended, ["o1"])
        XCTAssertTrue(sync.started.isEmpty)
    }

    func testCancelledOrderEnds() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 6))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertEqual(sync.ended, ["o1"])
    }

    func testPendingOrderNeitherStartsNorEnds() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 1))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertTrue(sync.started.isEmpty)
        XCTAssertTrue(sync.ended.isEmpty)
    }
}
