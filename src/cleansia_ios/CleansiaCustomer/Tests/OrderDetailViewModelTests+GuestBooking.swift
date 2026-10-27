import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

private final class LiveActivityStartSpy: OrderLiveActivitySyncing, @unchecked Sendable {
    private(set) var startedOrderIds: [String] = []
    private(set) var updatedOrderIds: [String] = []

    func start(orderId: String, orderNumber _: String, status _: String, window _: EtaWindow) {
        startedOrderIds.append(orderId)
    }

    func update(orderId: String, orderNumber _: String, status _: String, window _: EtaWindow) {
        updatedOrderIds.append(orderId)
    }

    func end(orderId _: String, orderNumber _: String, status _: LiveActivityTerminalStatus) {}
}

@MainActor
extension OrderDetailViewModelTests {
    // MARK: - A booking made without an account, listed under the customer's e-mail

    private static let start = Date(timeIntervalSince1970: 1_800_000_000)

    private func booking(statusValue: Int, guest: Bool, staffed: Bool = false) -> CustomerOrderDetail {
        var order = OrderFixtures.detail(
            statusCode: Code(type: "OrderStatus", name: nil, value: statusValue),
            displayOrderNumber: "1042",
            cleaningDateTime: Self.start,
            estimatedMinutes: 120,
            currencyCode: "CZK",
            assignedEmployees: staffed
                ? [AssignedEmployeeDto(id: "seat-1", employeeId: "emp-1", fullName: "Jana", phoneNumber: nil)]
                : []
        )
        order.isGuestBooking = guest
        return order
    }

    /// The server refuses a guest booking's photos, so the detail never asks for them.
    func testAGuestBookingFetchesNoPhotos() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(booking(statusValue: 4, guest: true))]
        client.photosResults = [.success(OrderFixtures.photos())]
        let vm = makeVM(client: client)
        await vm.load()

        await vm.ensurePhotosLoaded()

        XCTAssertEqual(client.photosCallCount, 0)
        XCTAssertNil(vm.photos.loadedResponse)
    }

    func testAnAccountBookingStillFetchesItsPhotos() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(booking(statusValue: 4, guest: false))]
        client.photosResults = [.success(OrderFixtures.photos())]
        let vm = makeVM(client: client)
        await vm.load()

        await vm.ensurePhotosLoaded()

        XCTAssertEqual(client.photosCallCount, 1)
    }

    /// No push updates a guest booking and the server refuses its Live Activity registration, so the app
    /// starts no lock-screen card for it.
    func testAGuestBookingUnderWayStartsNoLiveActivity() async {
        for status in [3, 4] {
            let client = FakeOrderClient()
            client.detailResults = [.success(booking(statusValue: status, guest: true))]
            let spy = LiveActivityStartSpy()
            let vm = makeVM(client: client, liveActivity: spy)

            await vm.load()

            XCTAssertEqual(spy.startedOrderIds, [], "status \(status)")
            XCTAssertEqual(spy.updatedOrderIds, [], "status \(status)")
        }
    }

    func testAnAccountBookingUnderWayStillStartsItsLiveActivity() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(booking(statusValue: 4, guest: false))]
        let spy = LiveActivityStartSpy()
        let vm = makeVM(client: client, liveActivity: spy)

        await vm.load()

        XCTAssertEqual(spy.startedOrderIds, ["o1"])
    }

    /// Past the start with a cleaner on it, a guest booking offers no no-show report (the server refuses
    /// the dispute) and still no cancel (the server refuses that too).
    func testAStaffedGuestBookingPastItsStartOffersNeitherTheNoShowReportNorCancel() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(booking(statusValue: 2, guest: true, staffed: true))]
        let vm = makeVM(client: client, now: { Self.start.addingTimeInterval(60) })
        await vm.load()

        XCTAssertFalse(vm.canReportCleanerNoShow)
        XCTAssertFalse(vm.canCancel)
    }

    func testAGuestBookingIsCancellableBeforeItsStart() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(booking(statusValue: 2, guest: true, staffed: true))]
        let vm = makeVM(client: client, now: { Self.start.addingTimeInterval(-60) })
        await vm.load()

        XCTAssertTrue(vm.canCancel)
        XCTAssertFalse(vm.canReportCleanerNoShow)
    }

    func testAStaffedAccountBookingPastItsStartStillOffersTheNoShowReport() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(booking(statusValue: 2, guest: false, staffed: true))]
        let vm = makeVM(client: client, now: { Self.start.addingTimeInterval(60) })
        await vm.load()

        XCTAssertTrue(vm.canReportCleanerNoShow)
        XCTAssertFalse(vm.canCancel)
    }
}
