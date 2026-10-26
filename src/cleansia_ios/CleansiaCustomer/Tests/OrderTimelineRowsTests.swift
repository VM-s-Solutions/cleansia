import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

final class OrderTimelineRowsTests: XCTestCase {
    private let start = Date(timeIntervalSince1970: 1_800_000_000)

    private func at(_ minutes: Double) -> Date {
        start.addingTimeInterval(minutes * 60)
    }

    private func statuses(_ rows: [OrderTimelineCard.Row]) -> [Int?] {
        rows.map { $0.entry.status?.value }
    }

    func testARepeatedStatusIsListedOnceAtTheMomentItWasReached() {
        let rows = OrderTimelineCard.rows([
            OrderFixtures.track(statusValue: 0, createdOn: at(0)),
            OrderFixtures.track(statusValue: 2, createdOn: at(10)),
            OrderFixtures.track(statusValue: 2, createdOn: at(20))
        ])

        XCTAssertEqual(statuses(rows), [0, 2])
        XCTAssertEqual(rows.last?.entry.createdOn, at(10))
        XCTAssertEqual(rows.map(\.isOfferedAgain), [false, false])
    }

    func testTheWalkBackRowIsTheOneMarkedOfferedAgain() {
        let rows = OrderTimelineCard.rows([
            OrderFixtures.track(statusValue: 0, createdOn: at(0)),
            OrderFixtures.track(statusValue: 2, createdOn: at(10)),
            OrderFixtures.track(statusValue: 0, createdOn: at(20))
        ])

        XCTAssertEqual(statuses(rows), [0, 2, 0])
        XCTAssertEqual(rows.map(\.isOfferedAgain), [false, false, true])
    }

    func testARetakeAfterTheWalkBackKeepsEveryPass() {
        let rows = OrderTimelineCard.rows([
            OrderFixtures.track(statusValue: 0, createdOn: at(0)),
            OrderFixtures.track(statusValue: 2, createdOn: at(10)),
            OrderFixtures.track(statusValue: 0, createdOn: at(20)),
            OrderFixtures.track(statusValue: 2, createdOn: at(30))
        ])

        XCTAssertEqual(statuses(rows), [0, 2, 0, 2])
        XCTAssertEqual(rows.map(\.isOfferedAgain), [false, false, true, false])
    }

    func testASeatReadvertisedBeforeTheWalkBackStillMarksTheWalkBack() {
        let rows = OrderTimelineCard.rows([
            OrderFixtures.track(statusValue: 0, createdOn: at(0)),
            OrderFixtures.track(statusValue: 2, createdOn: at(10)),
            OrderFixtures.track(statusValue: 2, createdOn: at(15)),
            OrderFixtures.track(statusValue: 0, createdOn: at(20))
        ])

        XCTAssertEqual(statuses(rows), [0, 2, 0])
        XCTAssertEqual(rows.map(\.isOfferedAgain), [false, false, true])
    }

    func testTheFirstNewRowIsNeverMarked() {
        let rows = OrderTimelineCard.rows([
            OrderFixtures.track(statusValue: 0, createdOn: at(0)),
            OrderFixtures.track(statusValue: 6, createdOn: at(10))
        ])

        XCTAssertEqual(rows.map(\.isOfferedAgain), [false, false])
    }

    func testRowsAreSortedByTimeWithAnUndatedRowLast() {
        let undated = OrderStatusTrackDto(status: Code(type: "OrderStatus", name: nil, value: 5), createdOn: nil)
        let rows = OrderTimelineCard.rows([
            undated,
            OrderFixtures.track(statusValue: 3, createdOn: at(30)),
            OrderFixtures.track(statusValue: 0, createdOn: at(0)),
            OrderFixtures.track(statusValue: 2, createdOn: at(10))
        ])

        XCTAssertEqual(statuses(rows), [0, 2, 3, 5])
        XCTAssertNil(rows.last?.entry.createdOn)
    }

    func testAnEmptyHistoryHasNoRows() {
        XCTAssertTrue(OrderTimelineCard.rows([]).isEmpty)
    }
}
