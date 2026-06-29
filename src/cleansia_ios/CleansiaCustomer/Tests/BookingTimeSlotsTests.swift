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

        XCTAssertEqual(slots.count, 12)
        XCTAssertEqual(slots.first?.time, "08:00")
        XCTAssertEqual(slots.last?.time, "19:00")
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

    func testTodaySlotsBetweenTwoAndFourHoursAreExpress() {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 10, minute: 0))
        let slots = BookingTimeSlots.slots(for: now, now: now, calendar: calendar)

        XCTAssertEqual(slots.first { $0.time == "12:00" }?.state, .express)
        XCTAssertEqual(slots.first { $0.time == "13:00" }?.state, .express)
    }

    func testTodayFirstStandardSlotIsMarkedEarliestThenAvailable() {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 10, minute: 0))
        let slots = BookingTimeSlots.slots(for: now, now: now, calendar: calendar)

        XCTAssertEqual(slots.first { $0.time == "14:00" }?.state, .earliest)
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
        let earliest = try XCTUnwrap(slots.first { $0.state == .earliest })
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

    func testVisibleSlotsDropUnavailable() {
        let now = date(DateComponents(year: 2026, month: 7, day: 1, hour: 10, minute: 0))
        let slots = BookingTimeSlots.slots(for: now, now: now, calendar: calendar)
        let visible = slots.filter { $0.state != .unavailable }

        XCTAssertFalse(visible.contains { $0.state == .unavailable })
        XCTAssertEqual(visible.first?.time, "12:00")
    }
}
