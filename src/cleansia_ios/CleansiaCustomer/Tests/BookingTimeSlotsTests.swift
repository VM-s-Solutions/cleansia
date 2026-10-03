import XCTest
@testable import CleansiaCustomer

final class BookingTimeSlotsTests: XCTestCase {
    private let calendar = Calendar(identifier: .gregorian)

    private func date(_ components: DateComponents) -> Date {
        var copy = components
        copy.calendar = calendar
        copy.timeZone = TimeZone.current
        return calendar.date(from: copy) ?? Date()
    }

    func testBuildsTodayPlusSevenDays() {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 12))
        let days = BookingTimeSlots.days(now: now, calendar: calendar)

        XCTAssertEqual(days.count, 8)
        XCTAssertTrue(days[0].isToday)
        XCTAssertFalse(days[1].isToday)
        XCTAssertEqual(
            days[7].date,
            calendar.date(byAdding: .day, value: 7, to: now).map { calendar.startOfDay(for: $0) }
        )
    }

    func testFutureDayOffersEveryWindowAsAvailable() throws {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 12))
        let tomorrow = try XCTUnwrap(calendar.date(byAdding: .day, value: 1, to: now))
        let slots = BookingTimeSlots.slots(for: tomorrow, now: now, calendar: calendar)

        XCTAssertEqual(slots.count, 48)
        XCTAssertEqual(slots.first?.time, "08:00")
        XCTAssertEqual(slots.last?.time, "19:45")
        XCTAssertEqual(
            slots.filter { $0.time.hasPrefix("10:") }.map(\.time),
            ["10:00", "10:15", "10:30", "10:45"]
        )
        XCTAssertEqual(Set(slots.map(\.time)).count, slots.count)
        XCTAssertTrue(slots.allSatisfy { $0.state == .available })
    }

    func testTodayHidesSlotsUnderTwoHoursLead() {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 10, minute: 0))
        let slots = BookingTimeSlots.slots(for: now, now: now, calendar: calendar)

        let slot10 = slots.first { $0.time == "10:00" }
        let slot11 = slots.first { $0.time == "11:00" }
        XCTAssertEqual(slot10?.state, .unavailable)
        XCTAssertEqual(slot11?.state, .unavailable)
    }

    func testQuarterHoursKeepLeadTimeAndExpressBoundaries() {
        let now = date(DateComponents(year: 2026, month: 9, day: 10, hour: 10, minute: 15))
        let slots = BookingTimeSlots.slots(for: now, now: now, calendar: calendar)

        XCTAssertEqual(slots.first { $0.time == "12:00" }?.state, .unavailable)
        XCTAssertEqual(slots.first { $0.time == "12:15" }?.state, .express)
        XCTAssertEqual(slots.first { $0.time == "14:00" }?.state, .express)
        XCTAssertEqual(slots.first { $0.time == "14:15" }?.state, .available)
        XCTAssertEqual(slots.first { $0.time == "14:30" }?.state, .available)
    }

    func testSlotJustInsideLeadTimeIsUnavailable() {
        let now = date(DateComponents(year: 2026, month: 9, day: 10, hour: 10, minute: 15, second: 1))
        let slots = BookingTimeSlots.slots(for: now, now: now, calendar: calendar)

        XCTAssertEqual(slots.first { $0.time == "12:15" }?.state, .unavailable)
        XCTAssertEqual(slots.first { $0.time == "12:30" }?.state, .express)
    }

    func testTodaySlotsBetweenTwoAndFourHoursAreExpress() {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 10, minute: 0))
        let slots = BookingTimeSlots.slots(for: now, now: now, calendar: calendar)

        XCTAssertEqual(slots.first { $0.time == "12:00" }?.state, .express)
        XCTAssertEqual(slots.first { $0.time == "13:00" }?.state, .express)
    }

    /// The grid draws no "Earliest" tag any more (Android's twin dropped it with the list rows), so the
    /// first standard slot is simply available.
    func testTodayFirstStandardSlotIsAvailable() {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 10, minute: 0))
        let slots = BookingTimeSlots.slots(for: now, now: now, calendar: calendar)

        XCTAssertEqual(slots.first { $0.time == "14:00" }?.state, .available)
        XCTAssertEqual(slots.first { $0.time == "15:00" }?.state, .available)
        XCTAssertEqual(slots.first { $0.time == "19:00" }?.state, .available)
    }

    func testExpressBoundaryAlignsWithPricingSurchargeBand() throws {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 10, minute: 0))
        let slots = BookingTimeSlots.slots(for: now, now: now, calendar: calendar)

        for slot in slots where slot.state == .express {
            let instant = BookingTimeSlots.instant(date: now, timeLabel: slot.time, calendar: calendar)
            XCTAssertNotNil(instant)
            XCTAssertTrue(BookingPricing.requiresExpressSurcharge(cleaningAt: instant, now: now))
        }
        let earliest = try XCTUnwrap(slots.first { $0.state == .available })
        let earliestInstant = BookingTimeSlots.instant(date: now, timeLabel: earliest.time, calendar: calendar)
        XCTAssertFalse(BookingPricing.requiresExpressSurcharge(cleaningAt: earliestInstant, now: now))
    }

    func testInstantCombinesDateAndTimeLabel() throws {
        let day = date(DateComponents(year: 2026, month: 7, day: 4))
        let instant = BookingTimeSlots.instant(date: day, timeLabel: "09:00", calendar: calendar)

        XCTAssertNotNil(instant)
        let parts = try calendar.dateComponents([.year, .month, .day, .hour, .minute], from: XCTUnwrap(instant))
        XCTAssertEqual(parts.year, 2026)
        XCTAssertEqual(parts.month, 7)
        XCTAssertEqual(parts.day, 4)
        XCTAssertEqual(parts.hour, 9)
        XCTAssertEqual(parts.minute, 0)
    }

    func testInstantRejectsMalformedLabel() {
        let day = date(DateComponents(year: 2026, month: 7, day: 4))
        XCTAssertNil(BookingTimeSlots.instant(date: day, timeLabel: "9am", calendar: calendar))
        XCTAssertNil(BookingTimeSlots.instant(date: day, timeLabel: "", calendar: calendar))
    }

    func testQuarterHourMinutesSurviveConversionToUtc() throws {
        var prague = Calendar(identifier: .gregorian)
        prague.timeZone = try XCTUnwrap(TimeZone(identifier: "Europe/Prague"))
        let formatter = ISO8601DateFormatter()
        let day = try XCTUnwrap(formatter.date(from: "2026-09-10T00:00:00Z"))

        for minute in [15, 45] {
            let instant = try XCTUnwrap(
                BookingTimeSlots.instant(date: day, timeLabel: "10:\(minute)", calendar: prague)
            )
            XCTAssertEqual(formatter.string(from: instant), "2026-09-10T08:\(minute):00Z")
        }
    }

    // MARK: - The part of day (the web wizard's dayParts)

    func testEveryArrivalTimeFallsInExactlyOnePartSixteenToAPart() throws {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 12))
        let tomorrow = try XCTUnwrap(calendar.date(byAdding: .day, value: 1, to: now))
        let slots = BookingTimeSlots.slots(for: tomorrow, now: now, calendar: calendar)
        let parts = BookingTimeSlots.dayParts(slots)

        XCTAssertEqual(parts.map(\.part), [.morning, .afternoon, .evening])
        XCTAssertEqual(parts.flatMap { $0.slots.map(\.time) }, slots.map(\.time))
        XCTAssertEqual(parts.map(\.slots.count), [16, 16, 16])
        XCTAssertEqual(parts.map { "\($0.slots.first?.time ?? "")–\($0.slots.last?.time ?? "")" }, [
            "08:00–11:45", "12:00–15:45", "16:00–19:45"
        ])
    }

    func testAFutureDayOpensOnThePartHoldingTheBookedTime() throws {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 12))
        let tomorrow = try XCTUnwrap(calendar.date(byAdding: .day, value: 1, to: now))
        let parts = BookingTimeSlots.dayParts(BookingTimeSlots.slots(for: tomorrow, now: now, calendar: calendar))

        XCTAssertEqual(BookingTimeSlots.openingDayPart(parts, selectedTime: "09:00"), .morning)
        XCTAssertEqual(BookingTimeSlots.openingDayPart(parts, selectedTime: "15:45"), .afternoon)
        XCTAssertEqual(BookingTimeSlots.openingDayPart(parts, selectedTime: "16:00"), .evening)
        // No time booked yet: the first part with a bookable slot, as the web's default 09:00 lands.
        XCTAssertEqual(BookingTimeSlots.openingDayPart(parts, selectedTime: ""), .morning)
    }

    /// At 13:00 every morning slot is inside the lead time, so the morning is disabled and the step opens
    /// on the afternoon — the web wizard's "snap to 15:00, morning closed" case.
    func testAtOnePmTodayTheMorningIsClosedAndTheAfternoonOpens() {
        let now = date(DateComponents(year: 2026, month: 9, day: 10, hour: 13, minute: 0))
        let parts = BookingTimeSlots.dayParts(BookingTimeSlots.slots(for: now, now: now, calendar: calendar))

        XCTAssertFalse(parts[0].isBookable)
        XCTAssertEqual(parts[0].bookableCount, 0)
        XCTAssertTrue(parts[1].isBookable)
        // 15:00–15:45 are bookable (15:00–16:45 express, then standard): four of the afternoon's sixteen.
        XCTAssertEqual(parts[1].bookableCount, 4)
        XCTAssertEqual(parts[2].bookableCount, 16)
        XCTAssertEqual(BookingTimeSlots.openingDayPart(parts, selectedTime: ""), .afternoon)
        // A booked time in a closed part does not hold the step there.
        XCTAssertEqual(BookingTimeSlots.openingDayPart(parts, selectedTime: "09:00"), .afternoon)
    }

    /// A slot inside the lead time stays in the grid, drawn disabled, rather than vanishing from it.
    func testAPartKeepsItsUnavailableSlotsSoTheGridStaysFourByFour() {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 10, minute: 0))
        let parts = BookingTimeSlots.dayParts(BookingTimeSlots.slots(for: now, now: now, calendar: calendar))

        XCTAssertEqual(parts[1].slots.count, 16)
        XCTAssertEqual(parts[1].slots.first { $0.state != .unavailable }?.time, "12:00")
        XCTAssertEqual(parts[0].bookableCount, 0)
    }

    func testTimesOutsideTheWindowBelongToNoPart() {
        XCTAssertNil(DayPart.of("07:45"))
        XCTAssertNil(DayPart.of("20:00"))
        XCTAssertNil(DayPart.of(""))
        XCTAssertEqual(DayPart.of("08:00"), .morning)
        XCTAssertEqual(DayPart.of("11:45"), .morning)
        XCTAssertEqual(DayPart.of("12:00"), .afternoon)
        XCTAssertEqual(DayPart.of("19:45"), .evening)
    }

    // MARK: A resumed draft's time

    func testASlotsStateIsTheWhenSteps() throws {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 12))
        let yesterday = try XCTUnwrap(calendar.date(byAdding: .day, value: -1, to: now))
        let beyondTheStrip = try XCTUnwrap(calendar.date(byAdding: .day, value: 8, to: now))

        XCTAssertEqual(BookingTimeSlots.slotState(on: now, time: "13:00", now: now, calendar: calendar), .unavailable)
        XCTAssertEqual(BookingTimeSlots.slotState(on: now, time: "15:00", now: now, calendar: calendar), .express)
        XCTAssertEqual(BookingTimeSlots.slotState(on: now, time: "18:00", now: now, calendar: calendar), .available)
        XCTAssertNil(BookingTimeSlots.slotState(on: yesterday, time: "18:00", now: now, calendar: calendar))
        XCTAssertNil(BookingTimeSlots.slotState(on: beyondTheStrip, time: "18:00", now: now, calendar: calendar))
        XCTAssertNil(BookingTimeSlots.slotState(on: now, time: "18:07", now: now, calendar: calendar))
    }

    /// The When step's rules decide, at the moment the draft comes back: inside the lead time, past, or
    /// slid from the standard band into the express one (quoted without the surcharge) it is gone.
    func testADraftTimeHoldsOnlyWhileTheWhenStepStillOffersItInItsBand() {
        let left = date(DateComponents(year: 2026, month: 7, day: 1, hour: 12))
        let day = left
        let holds = { (time: String, now: Date) in
            BookingTimeSlots.draftTimeStillHolds(on: day, time: time, leftAt: left, now: now, calendar: self.calendar)
        }
        let moment = { (hour: Int, minute: Int) in
            self.date(DateComponents(year: 2026, month: 7, day: 1, hour: hour, minute: minute))
        }

        XCTAssertTrue(holds("18:00", moment(13, 0)), "a standard slot still standard holds")
        XCTAssertFalse(holds("18:00", moment(15, 0)), "a standard slot now in the express band was quoted without it")
        XCTAssertTrue(holds("15:00", moment(12, 30)), "an express slot still express holds")
        XCTAssertFalse(holds("15:00", moment(13, 30)), "an express slot now inside the lead time is gone")
        XCTAssertFalse(holds("18:00", moment(16, 30)), "a slot inside the lead time is gone")
        let nextMorning = date(DateComponents(year: 2026, month: 7, day: 2, hour: 9))
        XCTAssertFalse(holds("18:00", nextMorning), "a slot on a day that has passed is gone")
    }

    /// Tomorrow's slot is today's once midnight passes, and is judged by today's bands.
    func testTomorrowsSlotIsJudgedByTodaysBandsAfterMidnight() {
        let left = date(DateComponents(year: 2026, month: 7, day: 1, hour: 23))
        let tomorrow = date(DateComponents(year: 2026, month: 7, day: 2))
        let holds = { (now: Date) in
            BookingTimeSlots.draftTimeStillHolds(
                on: tomorrow,
                time: "08:00",
                leftAt: left,
                now: now,
                calendar: self.calendar
            )
        }

        XCTAssertTrue(holds(date(DateComponents(year: 2026, month: 7, day: 2, hour: 3))))
        XCTAssertFalse(holds(date(DateComponents(year: 2026, month: 7, day: 2, hour: 5))), "now in the express band")
        XCTAssertFalse(holds(date(DateComponents(year: 2026, month: 7, day: 2, hour: 7))), "now inside the lead time")
    }

    func testADraftWithNoTimeOrNoRecordOfLeavingHasOnlyTheLeadTimeToMeet() {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 12))
        XCTAssertTrue(BookingTimeSlots.draftTimeStillHolds(
            on: nil,
            time: "",
            leftAt: nil,
            now: now,
            calendar: calendar
        ))
        XCTAssertTrue(BookingTimeSlots.draftTimeStillHolds(
            on: now,
            time: "",
            leftAt: nil,
            now: now,
            calendar: calendar
        ))
        XCTAssertTrue(
            BookingTimeSlots.draftTimeStillHolds(on: now, time: "15:00", leftAt: nil, now: now, calendar: calendar)
        )
        XCTAssertFalse(
            BookingTimeSlots.draftTimeStillHolds(on: now, time: "13:00", leftAt: nil, now: now, calendar: calendar)
        )
    }
}
