import XCTest
@testable import CleansiaCustomer

@MainActor
extension OrderDetailViewModelTests {
    func testEventBusTriggersRefetch() async {
        let client = FakeOrderClient()
        client.detailResults = [
            .success(OrderFixtures.detail(statusValue: 5)),
            .success(OrderFixtures.detail(statusValue: 5))
        ]
        let bus = OrderEventBus()
        let vm = makeVM(client: client, eventBus: bus)
        await vm.load()

        bus.emit(orderId: "o1")
        try? await Task.sleep(nanoseconds: 30_000_000)

        XCTAssertGreaterThanOrEqual(client.detailCallCount, 2)
    }

    func testAnotherOrdersEventDoesNotRefetch() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 5))]
        let bus = OrderEventBus()
        let vm = makeVM(client: client, eventBus: bus)
        await vm.load()

        bus.emit(orderId: "o2")
        try? await Task.sleep(nanoseconds: 30_000_000)

        XCTAssertEqual(client.detailCallCount, 1)
    }
}
