import XCTest
@testable import CleansiaCustomer

/// Every basket validator refuses a home above `BookingPolicy.MaxRooms` / `MaxBathrooms`
/// (`order.size_exceeds_maximum`), so a stepper that goes one further offers a booking that cannot be
/// made. The caps are read from the policy itself, so the two cannot drift apart unnoticed. Android's
/// `PropertySizeTest` holds the same line.
final class PropertySizeTests: XCTestCase {
    func testTheRoomCapIsTheServers() throws {
        XCTAssertEqual(PropertySize.maxRooms, try Self.policyInt("MaxRooms"))
    }

    func testTheBathroomCapIsTheServers() throws {
        XCTAssertEqual(PropertySize.maxBathrooms, try Self.policyInt("MaxBathrooms"))
    }

    func testBothBookingFlowsCapTheirSteppers() throws {
        for path in [
            "CleansiaCustomer/Sources/Features/Booking/Steps/ServicesStep.swift",
            "CleansiaCustomer/Sources/Features/Recurring/CreateRecurringScreen.swift"
        ] {
            let source = try String(contentsOf: Self.iosRoot().appendingPathComponent(path), encoding: .utf8)
            XCTAssertTrue(source.contains("maximum: PropertySize.maxRooms"), "\(path) lets rooms run past the cap")
            XCTAssertTrue(
                source.contains("maximum: PropertySize.maxBathrooms"),
                "\(path) lets bathrooms run past the cap"
            )
        }
    }

    private static func policyInt(_ name: String) throws -> Int {
        let policy = iosRoot()
            .deletingLastPathComponent()
            .appendingPathComponent("Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs")
        let source = try String(contentsOf: policy, encoding: .utf8)
        let pattern = #"public\s+const\s+int\s+"# + name + #"\s*=\s*(\d+)\s*;"#
        let regex = try NSRegularExpression(pattern: pattern)
        let match = try XCTUnwrap(
            regex.firstMatch(in: source, range: NSRange(source.startIndex..., in: source)),
            "BookingPolicy.\(name) not found — the parser needs updating"
        )
        let digits = try XCTUnwrap(Range(match.range(at: 1), in: source))
        return try XCTUnwrap(Int(source[digits]))
    }

    private static func iosRoot() -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
    }
}
