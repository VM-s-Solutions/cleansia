import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

private final class SuccessLoadRecorder: @unchecked Sendable {
    var fetchedIds: [String] = []
    var warmCount = 0
}

@MainActor
final class BookingSuccessViewModelTests: XCTestCase {
    private func makeVM(
        orderId: String = "o-1",
        order: OrderItem? = nil,
        recorder: SuccessLoadRecorder = SuccessLoadRecorder()
    ) -> BookingSuccessViewModel {
        BookingSuccessViewModel(
            orderId: orderId,
            fetch: { id in
                recorder.fetchedIds.append(id)
                return order
            },
            warmOrders: { recorder.warmCount += 1 }
        )
    }

    func testStartsLoadingSoTheCodePillRendersAlone() {
        let vm = makeVM()

        guard case .loading = vm.state else { return XCTFail("expected .loading") }
        XCTAssertNil(vm.order)
    }

    func testLoadSuccessPopulatesTheSummaryOrder() async {
        let order = OrderItem(id: "o-1", totalPrice: 1200, confirmationCode: "CLN-777")
        let vm = makeVM(order: order)

        await vm.load()

        guard case let .loaded(loaded) = vm.state else { return XCTFail("expected .loaded") }
        XCTAssertEqual(loaded, order)
        XCTAssertEqual(vm.order, order)
    }

    func testFetchFailureDegradesToErrorNotABlankScreen() async {
        let vm = makeVM(order: nil)

        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected .error") }
        XCTAssertNil(vm.order)
    }

    func testBlankOrderIdSkipsTheFetchAndDegrades() async {
        let recorder = SuccessLoadRecorder()
        let vm = makeVM(orderId: " ", recorder: recorder)

        await vm.load()

        XCTAssertTrue(recorder.fetchedIds.isEmpty)
        guard case .error = vm.state else { return XCTFail("expected .error") }
    }

    func testLoadIsSingleFlight() async {
        let recorder = SuccessLoadRecorder()
        let vm = makeVM(order: OrderItem(id: "o-1"), recorder: recorder)

        await vm.load()
        await vm.load()

        XCTAssertEqual(recorder.fetchedIds, ["o-1"])
        XCTAssertEqual(recorder.warmCount, 1)
    }

    func testLoadWarmsTheOrdersCacheEvenWhenTheFetchIsSkipped() async {
        let recorder = SuccessLoadRecorder()
        let vm = makeVM(orderId: "", recorder: recorder)

        await vm.load()

        XCTAssertEqual(recorder.warmCount, 1)
    }

    func testEffectiveCodePrefersTheLoadedOrdersCode() async {
        let vm = makeVM(order: OrderItem(confirmationCode: "SERVER-1"))

        await vm.load()

        XCTAssertEqual(vm.effectiveCode(fallback: "NAV-1"), "SERVER-1")
    }

    func testEffectiveCodeFallsBackWhileLoadingAndWhenTheLoadedCodeIsBlank() async {
        let vm = makeVM(order: OrderItem(confirmationCode: " "))

        XCTAssertEqual(vm.effectiveCode(fallback: "NAV-1"), "NAV-1")
        await vm.load()
        XCTAssertEqual(vm.effectiveCode(fallback: "NAV-1"), "NAV-1")
    }
}
