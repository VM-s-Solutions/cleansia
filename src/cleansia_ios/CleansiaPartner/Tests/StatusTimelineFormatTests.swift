import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class StatusTimelineFormatTests: XCTestCase {
    private func track(_ value: Int, _ name: String?, _ secondsFromEpoch: TimeInterval?) -> OrderStatusTrackDto {
        OrderStatusTrackDto(
            status: Code(name: name, value: value),
            createdOn: secondsFromEpoch.map { Date(timeIntervalSince1970: $0) }
        )
    }

    func testSortsAscendingByCreatedOnWithLocalizedLabels() {
        let history = [
            track(4, "InProgress", 300),
            track(2, "Confirmed", 100),
            track(3, "OnTheWay", 200)
        ]
        let entries = StatusTimelineFormat.entries(from: history)
        XCTAssertEqual(
            entries.map(\.label),
            [L10n.Orders.statusLabel(._2), L10n.Orders.statusLabel(._3), L10n.Orders.statusLabel(._4)]
        )
    }

    func testMarksLastAsCurrentRestPast() {
        let history = [
            track(2, "Confirmed", 100),
            track(3, "OnTheWay", 200)
        ]
        let entries = StatusTimelineFormat.entries(from: history)
        XCTAssertEqual(entries.map(\.isCurrent), [false, true])
    }

    func testDropsEntriesWithoutTimestamp() {
        let history = [
            track(2, "Confirmed", 100),
            track(3, "OnTheWay", nil)
        ]
        let entries = StatusTimelineFormat.entries(from: history)
        XCTAssertEqual(entries.count, 1)
        XCTAssertEqual(entries.first?.label, L10n.Orders.statusLabel(._2))
    }

    func testEmptyHistoryYieldsNoEntries() {
        XCTAssertTrue(StatusTimelineFormat.entries(from: []).isEmpty)
    }

    func testUsesValueFallbackWhenNameMissing() {
        let entries = StatusTimelineFormat.entries(from: [track(5, nil, 100)])
        XCTAssertEqual(entries.first?.label, L10n.Orders.statusLabel(._5))
    }
}
