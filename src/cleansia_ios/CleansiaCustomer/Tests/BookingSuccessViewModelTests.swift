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
        order: CustomerOrderDetail? = nil,
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
        let order = OrderFixtures.detail(id: "o-1", total: 1200)
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
        let vm = makeVM(order: OrderFixtures.detail(id: "o-1"), recorder: recorder)

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

    /// Credit applies only to a card booking, which reaches the confirmation only once the PaymentSheet
    /// completed, while the one read of the order usually still says Pending (the webhook moves it to
    /// Paid). So the card line says "Paid by card" there, not the order detail's `cardShareLabel`.
    func testACreditSplitReadsPaidByCardWhileTheWebhookHasNotLanded() {
        let order = OrderFixtures.detail(
            total: 1500,
            creditAppliedAmount: 320,
            amountDueOnCard: 1180,
            paymentType: Code(type: "PaymentType", name: nil, value: 2),
            paymentStatus: Code(type: "PaymentStatus", name: nil, value: 1),
            currencyCode: "CZK"
        )

        let rows = BookingSuccessView.summaryRows(order, locale: Locale(identifier: "en"))

        XCTAssertEqual(order.cardShareLabel, L10n.Credit.dueOnCard)
        XCTAssertEqual(
            rows.map(\.label),
            [L10n.Booking.successTotalLabel, L10n.Credit.paidWithCredit, L10n.Credit.paidByCard]
        )
        XCTAssertEqual(rows.last?.value, OrdersFormat.price(1180, currencyCode: "CZK"))
    }
}
