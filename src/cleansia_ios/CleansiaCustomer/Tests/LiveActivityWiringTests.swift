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
    struct Call: Equatable {
        let orderId: String
        let orderNumber: String
        let status: String
        let window: EtaWindow
    }

    struct EndCall: Equatable {
        let orderId: String
        let orderNumber: String
        let status: LiveActivityTerminalStatus
    }

    private(set) var started: [Call] = []
    private(set) var updated: [Call] = []
    private(set) var ended: [EndCall] = []

    func start(orderId: String, orderNumber: String, status: String, window: EtaWindow) {
        started.append(Call(orderId: orderId, orderNumber: orderNumber, status: status, window: window))
    }

    func update(orderId: String, orderNumber: String, status: String, window: EtaWindow) {
        updated.append(Call(orderId: orderId, orderNumber: orderNumber, status: status, window: window))
    }

    func end(orderId: String, orderNumber: String, status: LiveActivityTerminalStatus) {
        ended.append(EndCall(orderId: orderId, orderNumber: orderNumber, status: status))
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

    private func order(statusValue: Int, history: [OrderStatusTrackDto]? = nil) -> OrderItem {
        OrderItem(
            id: "o1",
            displayOrderNumber: "1042",
            cleaningDateTime: start,
            estimatedTime: 90,
            orderStatus: Code(type: "OrderStatus", name: nil, value: statusValue),
            statusHistory: history
        )
    }

    private func makeVM(_ client: FakeOrderClient, sync: SpyLiveActivitySync) -> OrderDetailViewModel {
        OrderDetailViewModel(
            orderId: "o1",
            client: client,
            repository: OrderRepository(client: client),
            membershipRepository: MembershipRepository(client: FakeMembershipManagementClient()),
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
        XCTAssertEqual(sync.started.first?.status, "onTheWay")
        XCTAssertEqual(sync.started.first?.window.scheduledStart, start)
        XCTAssertEqual(sync.started.first?.window.scheduledEnd, start.addingTimeInterval(90 * 60))
        XCTAssertTrue(sync.ended.isEmpty)
    }

    func testInProgressOrderStartsAndUpdatesToCleaning() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 4))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        // The activity is started AND updated with "inProgress" so an already-cleaning order (or an
        // OnTheWay → InProgress transition on a re-fetch) renders "Cleaning in progress", not "On the way".
        XCTAssertEqual(sync.started.first?.status, "inProgress")
        XCTAssertEqual(sync.updated.first?.status, "inProgress")
        XCTAssertTrue(sync.ended.isEmpty)
    }

    func testInProgressWindowCarriesTheActualStartOffTheStatusHistory() async {
        let startedAt = start.addingTimeInterval(15 * 60)
        let client = FakeOrderClient()
        client.detailResults = [.success(order(
            statusValue: 4,
            history: [OrderFixtures.track(statusValue: 4, createdOn: startedAt)]
        ))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertEqual(sync.started.first?.window.phaseStart, startedAt)
        XCTAssertEqual(sync.started.first?.window.phaseEnd, startedAt.addingTimeInterval(90 * 60))
    }

    /// The terminal status must travel with the end — it is what the ended card is left showing. Ending
    /// without one leaves the card on its last in-service state, which the system then draws as a stale
    /// placeholder.
    func testCompletedOrderEndsWithTheCompletedStatus() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 5))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertEqual(sync.ended, [.init(orderId: "o1", orderNumber: "1042", status: .completed)])
        XCTAssertTrue(sync.started.isEmpty)
    }

    func testCancelledOrderEndsWithTheCancelledStatus() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 6))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertEqual(sync.ended, [.init(orderId: "o1", orderNumber: "1042", status: .cancelled)])
    }

    /// The order number is the only identity a system-restored / server-started card carries
    /// (`CleanOrderAttributes` holds no order id), so the end must pass it through or such a card is never
    /// resolved and never ended.
    func testEndCarriesTheOrderNumberSoARestoredCardCanBeResolved() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 5))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertEqual(sync.ended.first?.orderNumber, "1042")
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
