import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

final class GeneratedDateDecodingTests: XCTestCase {
    private struct Box: Decodable {
        let date: Date
    }

    // The exact composition CustomerAppContainer installs into
    // CodableHelper.jsonDecoder: the generated ISO chain + offset-less UTC fallback.
    private func decode(_ value: String) throws -> Date {
        let decoder = ApiDateDecoding.decoder(primary: { CodableHelper.dateFormatter.date(from: $0) })
        return try decoder.decode(Box.self, from: Data(#"{"date":"\#(value)"}"#.utf8)).date
    }

    private func reference() throws -> Date {
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
        XCTAssertEqual(try decode("2026-07-02T10:11:12"), try reference())
    }

    func testOffsetlessThreeDigitFractionDecodesAsUtc() throws {
        let decoded = try decode("2026-07-02T10:11:12.345")
        XCTAssertEqual(try decoded.timeIntervalSince(reference()), 0.345, accuracy: 0.001)
    }

    func testOffsetlessSevenDigitFractionDecodesAsUtc() throws {
        let decoded = try decode("2026-07-02T10:11:12.3456789")
        XCTAssertEqual(try decoded.timeIntervalSince(reference()), 0.345, accuracy: 0.001)
    }

    func testZuluDateTimeDecodes() throws {
        XCTAssertEqual(try decode("2026-07-02T10:11:12Z"), try reference())
    }

    func testZuluSevenDigitFractionDecodes() throws {
        let decoded = try decode("2026-07-02T10:11:12.3456789Z")
        XCTAssertEqual(try decoded.timeIntervalSince(reference()), 0.345, accuracy: 0.001)
    }

    func testDateOnlyDecodes() throws {
        let midnight = try reference().addingTimeInterval(-(10 * 3600 + 11 * 60 + 12))
        XCTAssertEqual(try decode("2026-07-02"), midnight)
    }
}
