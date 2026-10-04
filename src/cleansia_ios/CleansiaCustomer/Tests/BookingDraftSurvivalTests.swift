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

    /// The day the When step's strip selects for the draft's day label when it is `now`, matched the way
    /// `WhenWhereStep.selectedDay` matches it.
    private func stripDay(_ vm: BookingViewModel, now: Date) -> Date? {
        BookingTimeSlots.days(now: now, calendar: calendar).first {
            BookingDateFormat.dayLabel($0.date, calendar: calendar, now: now) == vm.state.selectedDate
        }?.date
    }

    /// Only the server refused a resumed time that had come inside the lead time; now the draft goes back
    /// to the When step without it, keeping its day and everything else.
    func testAResumedTimeInsideTheLeadTimeIsClearedAndTheDraftGoesBackToWhen() {
        let vm = makeVM()
        draftOnConfirm(vm)
        XCTAssertEqual(vm.currentStep, 4)
        let resumedAt = at(day: 1, hour: 17)

        XCTAssertTrue(vm.revalidateResumedTime(now: resumedAt, calendar: calendar))

        XCTAssertEqual(vm.state.selectedTime, "")
        XCTAssertNil(vm.state.selectedInstant)
        XCTAssertEqual(stripDay(vm, now: resumedAt), at(day: 1, hour: 0), "the When step no longer selects the day")
        XCTAssertEqual(vm.currentStep, 3)
        XCTAssertEqual(vm.state.street, "Vodičkova 12")
        XCTAssertEqual(vm.state.selectedServiceIds, ["s-1"])
    }

    /// Picked the evening before, the day carries its weekday label, which on the day itself the strip gives
    /// to the same weekday a week later: a time chosen there booked next week, and Confirm still read the
    /// weekday. The kept day is the draft's own date, today.
    func testADayThatHasSinceBecomeTodayIsTheDayTheWhenStepSelects() {
        let vm = makeVM()
        seedDraft(vm)
        vm.selectDay(at(day: 2, hour: 0), calendar: calendar)
        vm.selectTime("09:00", on: at(day: 2, hour: 0), calendar: calendar)
        vm.advance()
        vm.draftLeft(at: at(day: 1, hour: 20))
        XCTAssertEqual(
            vm.state.selectedDate,
            BookingDateFormat.dayLabel(at(day: 2, hour: 0), calendar: calendar, now: at(day: 1, hour: 20)),
            "the day was picked as a weekday, not as today"
        )
        let resumedAt = at(day: 2, hour: 8)

        XCTAssertTrue(vm.revalidateResumedTime(now: resumedAt, calendar: calendar))

        XCTAssertEqual(vm.state.selectedTime, "")
        XCTAssertEqual(stripDay(vm, now: resumedAt), at(day: 2, hour: 0), "the When step selects next week's day")
        XCTAssertEqual(vm.state.selectedDate, L10n.Booking.today)
        XCTAssertEqual(vm.currentStep, 3)
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
        let resumedAt = at(day: 1, hour: 9)
        var before = vm.state
        before.selectedDate = BookingDateFormat.dayLabel(at(day: 1, hour: 0), calendar: calendar, now: resumedAt)

        XCTAssertFalse(vm.revalidateResumedTime(now: resumedAt, calendar: calendar))

        XCTAssertEqual(vm.state, before)
        XCTAssertEqual(vm.currentStep, 4)
    }

    /// A time that still holds on a day that has since become today kept the weekday it was picked as:
    /// Confirm read the weekday, and going back to When selected the same weekday a week later.
    func testAHeldTimeOnADayThatHasSinceBecomeTodayReadsToday() {
        let vm = makeVM()
        seedDraft(vm)
        vm.selectDay(at(day: 2, hour: 0), calendar: calendar)
        vm.selectTime("18:00", on: at(day: 2, hour: 0), calendar: calendar)
        vm.advance()
        vm.draftLeft(at: at(day: 1, hour: 20))
        let resumedAt = at(day: 2, hour: 8)

        XCTAssertFalse(vm.revalidateResumedTime(now: resumedAt, calendar: calendar))

        XCTAssertEqual(vm.state.selectedTime, "18:00")
        XCTAssertEqual(vm.state.selectedDate, L10n.Booking.today)
        XCTAssertEqual(stripDay(vm, now: resumedAt), at(day: 2, hour: 0), "the When step selects next week's day")
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

    // MARK: An open sheet's time — on the way back to the foreground and before submit

    /// `time` on 1 July, picked on the Confirm step and quoted at `quotedAt`.
    private func quotedOnConfirm(_ vm: BookingViewModel, time: String, quotedAt: Date) {
        seedDraft(vm)
        vm.selectDay(at(day: 1, hour: 0), calendar: calendar)
        vm.selectTime(time, on: at(day: 1, hour: 0), calendar: calendar)
        vm.advance()
        land(vm, at: quotedAt)
    }

    /// A quote for the selection on screen lands at `moment`.
    private func land(_ vm: BookingViewModel, at moment: Date) {
        vm.landQuote(
            BookingQuote(totalPrice: 1000, currencyCode: "CZK"),
            for: vm.state.quoteRequest(marketCountryId: nil),
            at: moment
        )
    }

    /// The sheet's way back to the foreground (`recheckOpenBooking`) is this call with the sheet open, when no
    /// close has been recorded: 18:00 was quoted standard at 13:00 and is express at 14:30.
    func testAnOpenSheetsTimeThatSlidIntoTheExpressBandSinceItWasQuotedIsCleared() {
        let vm = makeVM()
        quotedOnConfirm(vm, time: "18:00", quotedAt: at(day: 1, hour: 13))
        let back = at(day: 1, hour: 14, minute: 30)

        XCTAssertTrue(vm.revalidateResumedTime(now: back, calendar: calendar))

        XCTAssertEqual(vm.state.selectedTime, "")
        XCTAssertEqual(stripDay(vm, now: back), at(day: 1, hour: 0), "the day did not stay with the draft")
        XCTAssertEqual(vm.currentStep, 3)
    }

    /// 15:00 quoted at 10:30 is standard; at 12:30 it is express already, so the close calls it unchanged at 12:45.
    func testTheBandIsJudgedFromWhenTheQuoteLandedNotFromWhenTheSheetClosed() {
        let vm = makeVM()
        quotedOnConfirm(vm, time: "15:00", quotedAt: at(day: 1, hour: 10, minute: 30))
        vm.draftLeft(at: at(day: 1, hour: 12, minute: 30))

        XCTAssertTrue(vm.revalidateResumedTime(now: at(day: 1, hour: 12, minute: 45), calendar: calendar))

        XCTAssertEqual(vm.state.selectedTime, "")
        XCTAssertEqual(vm.currentStep, 3)
    }

    /// Closed at 08:00, when 14:30 was standard; reopened, and 14:30 picked and quoted express at 12:00.
    func testATimeQuotedAfterTheSheetLastClosedIsJudgedFromItsQuote() {
        let vm = makeVM()
        vm.draftLeft(at: at(day: 1, hour: 8))
        quotedOnConfirm(vm, time: "14:30", quotedAt: at(day: 1, hour: 12))

        XCTAssertFalse(vm.revalidateResumedTime(now: at(day: 1, hour: 12, minute: 10), calendar: calendar))

        XCTAssertEqual(vm.state.selectedTime, "14:30")
        XCTAssertEqual(vm.currentStep, 4)
    }

    /// A re-quote after 15:00 went express carries the surcharge, so from then on the time holds — neither the
    /// first quote nor the close before it is the band it is judged in.
    func testAReQuoteThatLandsLaterResetsTheMomentTheTimeIsJudgedFrom() {
        let vm = makeVM()
        quotedOnConfirm(vm, time: "15:00", quotedAt: at(day: 1, hour: 10, minute: 30))
        vm.draftLeft(at: at(day: 1, hour: 10, minute: 45))
        land(vm, at: at(day: 1, hour: 12))

        XCTAssertFalse(vm.revalidateResumedTime(now: at(day: 1, hour: 12, minute: 10), calendar: calendar))

        XCTAssertEqual(vm.state.selectedTime, "15:00")
    }

    /// The quote on screen names 18:00, so it says nothing about the 15:00 picked after it; the close does.
    func testAQuoteForAnotherTimeLeavesTheCloseToJudgeTheBand() {
        let vm = makeVM()
        quotedOnConfirm(vm, time: "18:00", quotedAt: at(day: 1, hour: 10))
        vm.selectTime("15:00", on: at(day: 1, hour: 0), calendar: calendar)
        vm.draftLeft(at: at(day: 1, hour: 12, minute: 30))

        XCTAssertFalse(vm.revalidateResumedTime(now: at(day: 1, hour: 12, minute: 45), calendar: calendar))

        XCTAssertEqual(vm.state.selectedTime, "15:00")
    }

    /// A seeded open resets the draft: the last booking's quote and close are not the band the next is judged in.
    func testResetForgetsWhenTheLastBookingWasQuotedAndLeft() {
        let vm = makeVM()
        quotedOnConfirm(vm, time: "18:00", quotedAt: at(day: 1, hour: 8))
        vm.draftLeft(at: at(day: 1, hour: 8))

        vm.reset()
        vm.selectDay(at(day: 1, hour: 0), calendar: calendar)
        vm.selectTime("15:00", on: at(day: 1, hour: 0), calendar: calendar)

        XCTAssertNil(vm.quotedAt)
        XCTAssertFalse(vm.revalidateResumedTime(now: at(day: 1, hour: 12, minute: 45), calendar: calendar))
        XCTAssertEqual(vm.state.selectedTime, "15:00")
    }

    /// The live quote records when it landed, and a selection emptied of everything drops it with the quote.
    func testALandedQuoteRecordsWhenItLandedAndAnEmptiedSelectionForgetsIt() async throws {
        let scheduler = TestScheduler.dispatch
        let vm = BookingViewModel(
            catalogClient: FakeCatalogClient(),
            quoteClient: FakeQuoteClient(),
            quoteDebounce: .milliseconds(400),
            scheduler: scheduler.eraseToAnyScheduler()
        )
        let before = Date()

        vm.update { current in
            var next = current
            next.selectedServiceIds = ["s-1"]
            return next
        }
        scheduler.advance(by: .milliseconds(400))
        await eventually { vm.quoteState.quote != nil }

        let landed = try XCTUnwrap(vm.quotedAt, "the landed quote recorded no moment")
        XCTAssertGreaterThanOrEqual(landed, before)
        XCTAssertLessThanOrEqual(landed, Date())

        vm.update { current in
            var next = current
            next.selectedServiceIds = []
            return next
        }
        scheduler.advance(by: .milliseconds(400))
        await eventually { vm.quoteState == .idle }

        XCTAssertNil(vm.quotedAt)
    }

    /// A signed-in customer's booking, with the fakes a submit reaches.
    private func makeSubmittingVM(
        quote: FakeQuoteClient = FakeQuoteClient(),
        profile: FakeProfileClient = FakeProfileClient(),
        create: FakeOrderCreateClient = FakeOrderCreateClient()
    ) -> BookingViewModel {
        BookingViewModel(
            quoteClient: quote,
            profileClient: profile,
            orderCreateClient: create,
            countryResolver: FakeCountryResolver(),
            savedCardClient: FakeSavedCardClient.holdingCzkCard(),
            tokenStore: FakeTokenStore.signedIn(),
            isCardPaymentAvailable: false,
            quoteDebounce: .milliseconds(400),
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler()
        )
    }

    /// `time` on the day `offset` days from today on the device's clock, which submit reads; card, on Confirm.
    private func pickOnConfirm(_ vm: BookingViewModel, time: String, dayOffset offset: Int) throws {
        seedDraft(vm)
        let calendar = Calendar.current
        let day = try XCTUnwrap(calendar.date(byAdding: .day, value: offset, to: calendar.startOfDay(for: Date())))
        vm.selectDay(day)
        vm.selectTime(time, on: day)
        vm.selectPayment(.card)
        vm.advance()
    }

    /// A Confirm step left on screen can hold a time that has since gone; the slide refuses it before anything
    /// is asked of the server, and the wizard is back on the When step to pick again.
    func testATimeThatNoLongerHoldsIsRefusedAtSubmitBeforeAnythingIsSent() async throws {
        let quote = FakeQuoteClient()
        let profile = FakeProfileClient()
        let create = FakeOrderCreateClient()
        let vm = makeSubmittingVM(quote: quote, profile: profile, create: create)
        try pickOnConfirm(vm, time: "10:00", dayOffset: -1)

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .timeNoLongerHolds)
        XCTAssertEqual(vm.state.selectedTime, "")
        XCTAssertEqual(vm.currentStep, 3)
        XCTAssertEqual(profile.callCount, 0)
        XCTAssertEqual(quote.callCount, 0)
        XCTAssertEqual(create.callCount, 0)
        XCTAssertEqual(vm.submitState, .idle)
    }

    func testATimeThatStillHoldsIsSent() async throws {
        let create = FakeOrderCreateClient(result: .success(CreatedOrder(id: "o-11", confirmationCode: "CLN-11")))
        let vm = makeSubmittingVM(create: create)
        try pickOnConfirm(vm, time: "10:00", dayOffset: 2)

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .success(orderId: "o-11", confirmationCode: "CLN-11"))
        XCTAssertEqual(create.callCount, 1)
    }

    private func eventually(_ condition: () -> Bool) async {
        for _ in 0 ..< 500 {
            if condition() { return }
            await Task.yield()
        }
    }

    /// The re-check runs on a plain open and on a return to the foreground with the sheet open — not on a
    /// seeded open — says why when it clears a time, and measures the band from the moment the sheet closed
    /// when no quote for the time landed.
    func testThePlainOpenAndTheForegroundReCheckTheTimeAndTheSheetsCloseRecordsWhenItWasLeft() throws {
        let shell = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/Features/Shell")
        let booking = try String(
            contentsOf: shell.appendingPathComponent("CustomerShellView+Booking.swift"),
            encoding: .utf8
        )
        func body(from start: String, to end: String?) -> String? {
            guard let open = booking.range(of: start) else { return nil }
            let close = end.flatMap { booking.range(of: $0, range: open.upperBound ..< booking.endIndex) }
            return String(booking[open.lowerBound ..< (close?.lowerBound ?? booking.endIndex)])
        }
        let plainOpen = try XCTUnwrap(
            body(from: "func openBooking() {", to: "func recheckOpenBooking"),
            "openBooking() not found"
        )
        let foreground = try XCTUnwrap(
            body(from: "func recheckOpenBooking() {", to: "func bookPackage"),
            "recheckOpenBooking() not found"
        )
        let seeded = try XCTUnwrap(body(from: "func bookPackage", to: nil), "bookPackage(_:) not found")
        for (entry, source) in [("a plain open", plainOpen), ("the return to the foreground", foreground)] {
            XCTAssertTrue(source.contains("if bookingVM.revalidateResumedTime() {"), "\(entry) no longer re-checks")
            XCTAssertTrue(
                source.contains("snackbar.showInfo(L10n.Booking.draftTimeChanged)"),
                "\(entry) clears the time unexplained"
            )
        }
        XCTAssertTrue(
            foreground.contains("bookingVM.objectWillChange.send()"),
            "the open sheet's When step is not redrawn against the clock on the way back"
        )
        XCTAssertFalse(seeded.contains("revalidateResumedTime"), "a seeded open starts afresh, so it re-checks nothing")

        let view = try String(contentsOf: shell.appendingPathComponent("CustomerShellView.swift"), encoding: .utf8)
            .replacingOccurrences(of: #"\s+"#, with: " ", options: .regularExpression)
        XCTAssertTrue(
            view.contains("bookingVM.draftLeft()"),
            "closing the sheet no longer records when the draft was left"
        )
        XCTAssertTrue(
            view.contains(
                ".onChange(of: scenePhase) { phase in " +
                    "if phase == .active, model.isBookingPresented { recheckOpenBooking() } }"
            ),
            "a return to the foreground no longer re-checks the open sheet"
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
