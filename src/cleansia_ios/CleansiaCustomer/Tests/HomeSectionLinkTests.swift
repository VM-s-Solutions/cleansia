import XCTest

/// Home's "See all" and "Manage" were bare text buttons about 19pt tall. They are the Core text link, which
/// carries the 44pt target in the same type and ink.
final class HomeSectionLinkTests: XCTestCase {
    func testTheRecentBookingsSeeAllIsAFullTouchTarget() throws {
        let source = try compactSource("CleansiaCustomer/Sources/Features/Home/HomeSecondarySections.swift")
        XCTAssertTrue(source.contains("CleansiaTextLink(L10n.Home.recentSeeAll,action:onSeeAll)"))
    }

    func testTheRecurringSchedulesManageIsAFullTouchTarget() throws {
        let source = try compactSource("CleansiaCustomer/Sources/Features/Home/HomeSectionViews.swift")
        XCTAssertTrue(source.contains("CleansiaTextLink(L10n.Home.recurringSectionManage,action:onManage)"))
    }

    private func compactSource(_ path: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(path), encoding: .utf8)
            .components(separatedBy: .whitespacesAndNewlines)
            .joined()
    }
}
