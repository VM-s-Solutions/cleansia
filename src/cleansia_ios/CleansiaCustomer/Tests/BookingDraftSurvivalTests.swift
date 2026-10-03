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
            orderClient: FakeOrderClient(),
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
            next.accessInstructions = "Key box by the gate, code 4321"
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
        XCTAssertEqual(vm.state.accessInstructions, "Key box by the gate, code 4321")
        XCTAssertEqual(vm.currentStep, 3)
    }

    func testResetIsTheOnlyDraftWipe() {
        let vm = makeVM()
        seedDraft(vm)

        vm.reset()

        XCTAssertEqual(vm.state, BookingState())
        XCTAssertEqual(vm.currentStep, 1)
    }

    // MARK: A resumed draft's time

    private let calendar = Calendar(identifier: .gregorian)

    private func at(day: Int, hour: Int, minute: Int = 0) -> Date {
        var components = DateComponents(year: 2026, month: 7, day: day, hour: hour, minute: minute)
        components.calendar = calendar
        components.timeZone = TimeZone.current
        return calendar.date(from: components) ?? Date()
    }

    /// A draft on the Confirm step holding today 18:00, left at 07:00.
    private func draftOnConfirm(_ vm: BookingViewModel) {
        seedDraft(vm)
        vm.selectDay(at(day: 1, hour: 0), calendar: calendar)
        vm.selectTime("18:00", on: at(day: 1, hour: 0), calendar: calendar)
        vm.advance()
        vm.draftLeft(at: at(day: 1, hour: 7))
    }

    /// Only the server refused a resumed time that had come inside the lead time; now the draft goes back
    /// to the When step without it, keeping its day and everything else.
    func testAResumedTimeInsideTheLeadTimeIsClearedAndTheDraftGoesBackToWhen() {
        let vm = makeVM()
        draftOnConfirm(vm)
        XCTAssertEqual(vm.currentStep, 4)
        let day = vm.state.selectedDate

        XCTAssertTrue(vm.revalidateResumedTime(now: at(day: 1, hour: 17), calendar: calendar))

        XCTAssertEqual(vm.state.selectedTime, "")
        XCTAssertNil(vm.state.selectedInstant)
        XCTAssertEqual(vm.state.selectedDate, day, "the day is still on the strip")
        XCTAssertEqual(vm.currentStep, 3)
        XCTAssertEqual(vm.state.street, "Vodičkova 12")
        XCTAssertEqual(vm.state.selectedServiceIds, ["s-1"])
    }

    func testAResumedTimeOnADayThatHasPassedTakesTheDayWithIt() {
        let vm = makeVM()
        draftOnConfirm(vm)

        XCTAssertTrue(vm.revalidateResumedTime(now: at(day: 2, hour: 9), calendar: calendar))

        XCTAssertEqual(vm.state.selectedTime, "")
        XCTAssertEqual(vm.state.selectedDate, "")
        XCTAssertEqual(vm.currentStep, 3)
    }

    func testAResumedStandardTimeNowInTheExpressBandIsCleared() {
        let vm = makeVM()
        draftOnConfirm(vm)

        XCTAssertTrue(vm.revalidateResumedTime(now: at(day: 1, hour: 15), calendar: calendar))
        XCTAssertEqual(vm.state.selectedTime, "")
    }

    func testAResumedTimeThatStillHoldsIsLeftAlone() {
        let vm = makeVM()
        draftOnConfirm(vm)
        let before = vm.state

        XCTAssertFalse(vm.revalidateResumedTime(now: at(day: 1, hour: 9), calendar: calendar))

        XCTAssertEqual(vm.state, before)
        XCTAssertEqual(vm.currentStep, 4)
    }

    func testADraftNotYetPastTheWhenStepStaysOnItsStep() {
        let vm = makeVM()
        vm.selectDay(at(day: 1, hour: 0), calendar: calendar)
        vm.selectTime("18:00", on: at(day: 1, hour: 0), calendar: calendar)
        vm.advance()

        XCTAssertTrue(vm.revalidateResumedTime(now: at(day: 1, hour: 17), calendar: calendar))

        XCTAssertEqual(vm.state.selectedTime, "")
        XCTAssertEqual(vm.currentStep, 2)
    }

    /// The re-check runs on a plain open only, says why when it clears a time, and measures the band from
    /// the moment the sheet closed.
    func testThePlainOpenReChecksTheTimeAndTheSheetsCloseRecordsWhenItWasLeft() throws {
        let shell = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/Features/Shell")
        let booking = try String(
            contentsOf: shell.appendingPathComponent("CustomerShellView+Booking.swift"),
            encoding: .utf8
        )
        let plainOpen = try XCTUnwrap(
            booking.range(of: "func openBooking() {").flatMap { start in
                booking.range(of: "func bookPackage", range: start.upperBound ..< booking.endIndex)
                    .map { String(booking[start.lowerBound ..< $0.lowerBound]) }
            },
            "openBooking() not found"
        )
        XCTAssertTrue(plainOpen.contains("if bookingVM.revalidateResumedTime() {"), "a plain open no longer re-checks")
        XCTAssertTrue(
            plainOpen.contains("snackbar.showInfo(L10n.Booking.draftTimeChanged)"),
            "the clear goes unexplained"
        )
        XCTAssertEqual(
            booking.components(separatedBy: "revalidateResumedTime()").count, 2,
            "only the plain open re-checks: a seeded one starts afresh"
        )
        let view = try String(contentsOf: shell.appendingPathComponent("CustomerShellView.swift"), encoding: .utf8)
        XCTAssertTrue(
            view.contains("bookingVM.draftLeft()"),
            "closing the sheet no longer records when the draft was left"
        )
    }

    func testTheNoticeResolvesInEveryLanguage() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for language in ["en", "cs", "sk", "uk", "ru"] {
            let path = try XCTUnwrap(
                [Bundle.main, Bundle(for: Self.self)].lazy
                    .compactMap { $0.path(forResource: language, ofType: "lproj") }.first
            )
            L10n.bundle = try XCTUnwrap(Bundle(path: path))
            let notice = L10n.Booking.draftTimeChanged
            XCTAssertFalse(notice.isEmpty || notice == "booking_draft_time_changed", "\(language) does not resolve")
        }
    }
}
