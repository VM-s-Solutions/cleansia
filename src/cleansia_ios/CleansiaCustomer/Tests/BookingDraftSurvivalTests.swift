import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// The booking VM is owned by the shell (session lifetime), not the sheet —
/// dismissing the sheet destroys only the view. These tests pin the seam: a
/// dismiss/reopen cycle (new sheet views over the same VM) must keep the
/// draft; only `reset()` — fired on submit success — wipes it.
@MainActor
final class BookingDraftSurvivalTests: XCTestCase {
    private func makeVM() -> BookingViewModel {
        let scheduler = TestScheduler.dispatch
        return BookingViewModel(
            catalogClient: FakeCatalogClient(),
            quoteClient: FakeQuoteClient(),
            quoteDebounce: .milliseconds(400),
            scheduler: scheduler.eraseToAnyScheduler()
        )
    }

    private func makeSheet(vm: BookingViewModel) -> BookingSheetView {
        BookingSheetView(
            vm: vm,
            geocoding: CLGeocoderGeocodingService(),
            mapProvider: PreviewMapProvider(),
            paymentSheet: FakePaymentSheetPresenter(),
            onDismiss: {},
            onViewOrder: { _ in },
            onCompleteProfile: {}
        )
    }

    private func seedDraft(_ vm: BookingViewModel) {
        vm.update { current in
            var next = current
            next.selectedServiceIds = ["s-1"]
            next.rooms = 3
            next.street = "Vodičkova 12"
            next.city = "Praha"
            next.zipCode = "11000"
            next.selectedDate = "Tomorrow"
            next.selectedTime = "10:00"
            next.promoCode = "WELCOME10"
            next.specialInstructions = "Ring twice"
            return next
        }
        vm.advance()
        vm.advance()
    }

    func testDraftSurvivesSheetDismissAndReopen() {
        let vm = makeVM()
        seedDraft(vm)

        var sheet: BookingSheetView? = makeSheet(vm: vm)
        _ = sheet
        sheet = nil
        _ = makeSheet(vm: vm)

        XCTAssertEqual(vm.state.selectedServiceIds, ["s-1"])
        XCTAssertEqual(vm.state.rooms, 3)
        XCTAssertEqual(vm.state.street, "Vodičkova 12")
        XCTAssertEqual(vm.state.city, "Praha")
        XCTAssertEqual(vm.state.selectedDate, "Tomorrow")
        XCTAssertEqual(vm.state.selectedTime, "10:00")
        XCTAssertEqual(vm.state.promoCode, "WELCOME10")
        XCTAssertEqual(vm.state.specialInstructions, "Ring twice")
        XCTAssertEqual(vm.currentStep, 3)
    }

    func testResetIsTheOnlyDraftWipe() {
        let vm = makeVM()
        seedDraft(vm)

        vm.reset()

        XCTAssertEqual(vm.state, BookingState())
        XCTAssertEqual(vm.currentStep, 1)
    }
}
