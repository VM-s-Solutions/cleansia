import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// Characterises the generated `OpenAPIDateWithoutTime` before trusting anything built on it, because
/// its defaulted `timezone: .current` is a trap: it stores the instant untouched and then, at encode
/// time, ADDS that zone's offset before formatting in GMT. A day that arrived as midnight UTC therefore
/// goes back out as the previous day from any handset west of Greenwich — and as itself from Prague and
/// from a UTC CI runner, which is why it has never been visible where this code is built.
final class CalendarDayWireTests: XCTestCase {
    private let newYork = TimeZone(identifier: "America/New_York")
    private let prague = TimeZone(identifier: "Europe/Prague")

    private func decoded(_ day: String) throws -> OpenAPIDateWithoutTime {
        try JSONDecoder().decode(OpenAPIDateWithoutTime.self, from: Data("\"\(day)\"".utf8))
    }

    private func encoded(_ value: OpenAPIDateWithoutTime) throws -> String {
        let data = try JSONEncoder().encode(value)
        return try XCTUnwrap(String(data: data, encoding: .utf8)).replacingOccurrences(of: "\"", with: "")
    }

    // MARK: - The defect, in the wrapper's own terms

    func testADayArrivesAsMidnightUtc() throws {
        let value = try decoded("1982-09-04")

        XCTAssertEqual(value.wrappedDate.timeIntervalSince1970, 399_945_600)
        XCTAssertEqual(value.timezone.secondsFromGMT(), 0)
    }

    func testRebuildingTheSameDayWestOfGreenwichSendsThePreviousDay() throws {
        let newYork = try XCTUnwrap(newYork)
        let arrived = try decoded("1982-09-04")

        let resent = OpenAPIDateWithoutTime(wrappedDate: arrived.wrappedDate, timezone: newYork)

        XCTAssertEqual(try encoded(resent), "1982-09-03")
    }

    /// Why neither a Prague desk nor a UTC runner has ever seen it: a positive offset moves midnight
    /// forward INTO the same day, so the string is unchanged.
    func testRebuildingTheSameDayEastOfGreenwichOrInGreenwichSendsTheSameDay() throws {
        let prague = try XCTUnwrap(prague)
        let arrived = try decoded("1982-09-04")

        XCTAssertEqual(
            try encoded(OpenAPIDateWithoutTime(wrappedDate: arrived.wrappedDate, timezone: prague)),
            "1982-09-04"
        )
        XCTAssertEqual(
            try encoded(OpenAPIDateWithoutTime(wrappedDate: arrived.wrappedDate, timezone: .gmt)),
            "1982-09-04"
        )
    }

    /// The severity question: one fixed offset, or one more day every time? The read always lands back
    /// on midnight UTC of whatever was stored, so the next save subtracts a further offset from a value
    /// that has already moved — the loss compounds, one day per save, without bound.
    func testEverySaveWestOfGreenwichLosesAnotherDay() throws {
        let newYork = try XCTUnwrap(newYork)
        var day = "1982-09-04"

        var sent: [String] = []
        for _ in 0 ..< 3 {
            let arrived = try decoded(day)
            day = try encoded(OpenAPIDateWithoutTime(wrappedDate: arrived.wrappedDate, timezone: newYork))
            sent.append(day)
        }

        XCTAssertEqual(sent, ["1982-09-03", "1982-09-02", "1982-09-01"])
    }

    /// Why no assertion comparing two of these can catch it: `==` looks only at `wrappedDate`, which the
    /// initializer stores untouched, so the zone that decides the encoded day is invisible to equality.
    func testEqualityIgnoresTheZoneThatDecidesTheEncodedDay() throws {
        let newYork = try XCTUnwrap(newYork)
        let arrived = try decoded("1982-09-04")

        let west = OpenAPIDateWithoutTime(wrappedDate: arrived.wrappedDate, timezone: newYork)
        let greenwich = OpenAPIDateWithoutTime(wrappedDate: arrived.wrappedDate, timezone: .gmt)

        XCTAssertEqual(west, greenwich)
        XCTAssertNotEqual(try encoded(west), try encoded(greenwich))
    }

    // MARK: - The one way this app builds one

    func testTheAppsInitializerPinsGreenwichWhereverTheHandsetIs() throws {
        let arrived = try decoded("1982-09-04")

        let value = try XCTUnwrap(OpenAPIDateWithoutTime(day: arrived.wrappedDate))

        XCTAssertEqual(value.timezone.secondsFromGMT(), 0)
        XCTAssertEqual(try encoded(value), "1982-09-04")
    }

    /// Reading the day in GMT rather than re-offsetting it is the whole of the fix: an instant late in a
    /// UTC day keeps that day instead of rolling into the next one under a positive device offset.
    func testAnInstantLateInTheDayStillSendsThatDay() throws {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = .gmt
        var components = DateComponents()
        components.year = 1982
        components.month = 9
        components.day = 4
        components.hour = 23
        components.minute = 30
        let lateInTheDay = try XCTUnwrap(calendar.date(from: components))

        let value = try XCTUnwrap(OpenAPIDateWithoutTime(day: lateInTheDay))

        XCTAssertEqual(try encoded(value), "1982-09-04")
    }

    func testNoDayMeansNoValue() {
        XCTAssertNil(OpenAPIDateWithoutTime(day: nil))
    }
}
