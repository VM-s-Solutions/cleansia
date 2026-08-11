import SwiftUI
import XCTest
@testable import CleansiaCore

/// A birth date is a day, not a moment, and the wire format carries no zone. This codebase writes one
/// instant for a day — midnight UTC — because the two things that produce one (a value decoded off the
/// wire, a value picked in a calendar) otherwise disagree about which instant means which day.
final class CalendarDayTests: XCTestCase {
    private let newYork = TimeZone(identifier: "America/New_York")
    private let prague = TimeZone(identifier: "Europe/Prague")

    func testAPickedDayBecomesMidnightUtcOfTheDayThatWasPicked() throws {
        let newYork = try XCTUnwrap(newYork)
        // 20:00 on the 10th in New York is already the 11th in UTC.
        let picked = try instant(year: 1990, month: 5, day: 10, hour: 20, in: newYork)

        let day = CalendarDay.startOfDay(picked, in: newYork)

        XCTAssertEqual(day, try instant(year: 1990, month: 5, day: 10, hour: 0, in: .gmt))
    }

    func testAnEarlyMorningPickEastOfGreenwichKeepsItsOwnDay() throws {
        let prague = try XCTUnwrap(prague)
        // 01:00 on the 10th in Prague is still the 9th in UTC.
        let picked = try instant(year: 1990, month: 5, day: 10, hour: 1, in: prague)

        let day = CalendarDay.startOfDay(picked, in: prague)

        XCTAssertEqual(day, try instant(year: 1990, month: 5, day: 10, hour: 0, in: .gmt))
    }

    /// The value that arrives from the server is already the normal form, so normalizing it must be a
    /// no-op — otherwise every read/write cycle would move it.
    func testNormalizingAValueThatIsAlreadyADayChangesNothing() throws {
        let day = try instant(year: 1990, month: 5, day: 10, hour: 0, in: .gmt)

        XCTAssertEqual(CalendarDay.startOfDay(day, in: .gmt), day)
        XCTAssertEqual(CalendarDay.startOfDay(CalendarDay.startOfDay(day, in: .gmt), in: .gmt), day)
    }

    /// Rendering a midnight-UTC instant in the device's zone prints the PREVIOUS day everywhere west of
    /// Greenwich, which is where a wrong birthday is first visible.
    func testADayReadsAsItselfRegardlessOfWhereTheHandsetIs() throws {
        let day = try instant(year: 1990, month: 5, day: 10, hour: 0, in: .gmt)
        let lastInstantOfTheSameDay = try instant(year: 1990, month: 5, day: 10, hour: 23, in: .gmt)

        XCTAssertEqual(CalendarDay.text(day, locale: Locale(identifier: "en_US")), "May 10, 1990")
        XCTAssertEqual(
            CalendarDay.text(lastInstantOfTheSameDay, locale: Locale(identifier: "en_US")),
            "May 10, 1990"
        )
    }

    /// The picker has to be shown the same zone the day is stored in, or it offers the day before.
    func testThePickerCalendarIsPinnedToGreenwich() {
        XCTAssertEqual(CalendarDay.calendar.timeZone.secondsFromGMT(), 0)
        XCTAssertEqual(CalendarDay.calendar.identifier, Calendar.current.identifier)
    }

    /// The picker is pinned to Greenwich, so the day it shows is the Greenwich day of the bound instant.
    /// Storing that instant unnormalized is what loses the day: the seed carries a wall-clock time that
    /// the encoder then re-reads in Greenwich.
    func testTheDayThePickerShowsIsTheDayThatIsStored() throws {
        var stored: Date?
        let binding = CalendarDay.pickerBinding(Binding(get: { stored }, set: { stored = $0 }))

        binding.wrappedValue = try instant(year: 1990, month: 5, day: 10, hour: 23, in: .gmt)

        XCTAssertEqual(stored, try instant(year: 1990, month: 5, day: 10, hour: 0, in: .gmt))
    }

    /// The onboarding case: the field starts nil, so the picker seeds from `now` and the value that
    /// reaches the wire is the seed's time of day unless it is dropped. Whatever hour it is here, none
    /// of it survives.
    func testAnUnsetFieldSeedsFromNowAndStoresThatDayWithNoTimeOfDay() throws {
        var stored: Date?
        let binding = CalendarDay.pickerBinding(Binding(get: { stored }, set: { stored = $0 }))

        let seed = binding.wrappedValue
        binding.wrappedValue = seed

        let parts = try CalendarDay.calendar.dateComponents(
            [.year, .month, .day, .hour, .minute, .second],
            from: XCTUnwrap(stored)
        )
        let seedDay = CalendarDay.calendar.dateComponents([.year, .month, .day], from: seed)
        XCTAssertEqual([parts.hour, parts.minute, parts.second], [0, 0, 0])
        XCTAssertEqual([parts.year, parts.month, parts.day], [seedDay.year, seedDay.month, seedDay.day])
    }

    func testAStoredDayIsHandedBackToThePickerUnchanged() throws {
        let day = try instant(year: 1990, month: 5, day: 10, hour: 0, in: .gmt)
        var stored: Date? = day
        let binding = CalendarDay.pickerBinding(Binding(get: { stored }, set: { stored = $0 }))

        XCTAssertEqual(binding.wrappedValue, day)
    }

    private func instant(year: Int, month: Int, day: Int, hour: Int, in timeZone: TimeZone) throws -> Date {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = timeZone
        var components = DateComponents()
        components.year = year
        components.month = month
        components.day = day
        components.hour = hour
        return try XCTUnwrap(calendar.date(from: components))
    }
}
