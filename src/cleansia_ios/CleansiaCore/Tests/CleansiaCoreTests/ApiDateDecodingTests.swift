import XCTest
@testable import CleansiaCore

final class ApiDateDecodingTests: XCTestCase {
    private struct Box: Decodable {
        let date: Date
    }

    /// Replicates the generated OpenISO8601DateFormatter chain the apps install
    /// as `primary`: fraction+offset, no-fraction+offset, date-only.
    private static let generatedChain: [DateFormatter] = [
        "yyyy-MM-dd'T'HH:mm:ss.SSSZZZZZ",
        "yyyy-MM-dd'T'HH:mm:ssZZZZZ",
        "yyyy-MM-dd"
    ].map { format in
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .iso8601)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = format
        return formatter
    }

    private func decode(_ value: String) throws -> Date {
        let decoder = ApiDateDecoding.decoder(primary: { raw in
            for formatter in Self.generatedChain {
                if let date = formatter.date(from: raw) {
                    return date
                }
            }
            return nil
        })
        return try decoder.decode(Box.self, from: Data(#"{"date":"\#(value)"}"#.utf8)).date
    }

    private func noonUtc() throws -> Date {
        var components = DateComponents()
        components.calendar = Calendar(identifier: .iso8601)
        components.timeZone = TimeZone(secondsFromGMT: 0)
        components.year = 2026
        components.month = 7
        components.day = 2
        components.hour = 10
        components.minute = 11
        components.second = 12
        return try XCTUnwrap(components.date)
    }

    func testOffsetlessDateTimeDecodesAsUtc() throws {
        XCTAssertEqual(try decode("2026-07-02T10:11:12"), try noonUtc())
    }

    func testOffsetlessDateTimeWithSevenDigitFractionDecodesAsUtc() throws {
        let decoded = try decode("2026-07-02T10:11:12.3456789")
        XCTAssertEqual(try decoded.timeIntervalSince(noonUtc()), 0.345, accuracy: 0.001)
    }

    func testZuluDateTimeDecodesThroughThePrimaryChain() throws {
        XCTAssertEqual(try decode("2026-07-02T10:11:12Z"), try noonUtc())
    }

    func testZuluDateTimeWithSevenDigitFractionDecodesThroughThePrimaryChain() throws {
        let decoded = try decode("2026-07-02T10:11:12.3456789Z")
        XCTAssertEqual(try decoded.timeIntervalSince(noonUtc()), 0.345, accuracy: 0.001)
    }

    func testDateOnlyDecodesThroughThePrimaryChain() throws {
        let midnight = try noonUtc().addingTimeInterval(-(10 * 3600 + 11 * 60 + 12))
        XCTAssertEqual(try decode("2026-07-02"), midnight)
    }

    func testPrimaryParserWinsOverTheFallback() throws {
        let fixed = Date(timeIntervalSince1970: 42)
        let decoder = ApiDateDecoding.decoder(primary: { _ in fixed })
        let decoded = try decoder.decode(Box.self, from: Data(#"{"date":"2026-07-02T10:11:12"}"#.utf8))
        XCTAssertEqual(decoded.date, fixed)
    }

    func testUnparseableDateThrows() {
        XCTAssertThrowsError(try decode("not-a-date"))
    }
}
