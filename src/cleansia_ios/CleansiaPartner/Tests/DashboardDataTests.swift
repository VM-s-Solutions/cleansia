import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The hero answers "what is my next job" before "what work is there": Android's `pickNextJob` takes the
/// soonest upcoming order the cleaner is on (Confirmed, On the way or In progress) over the board's preview,
/// and the greeting counts the upcoming orders dated today (`filterTodaysJobs`).
final class DashboardDataTests: XCTestCase {
    private static let now = Calendar.current.date(from: DateComponents(year: 2026, month: 11, day: 9, hour: 10))
        ?? Date()

    private static let board = AvailableJobsPreview(totalAvailableCount: 3, totalPotentialEarnings: 900)

    func testTheSoonestActiveUpcomingOrderIsTheHeroEvenWithWorkOnTheBoard() {
        let data = from(preview: Self.board, upcoming: [
            .upcoming(id: "new", status: 0, startsAt: hours(0.5)),
            .upcoming(id: "completed", status: 5, startsAt: hours(0.75)),
            .upcoming(id: "in-progress", status: 4, startsAt: hours(5)),
            .upcoming(id: "confirmed", status: 2, startsAt: hours(2), customerName: "Jana", address: "Praha 5"),
            .upcoming(id: "on-the-way", status: 3, startsAt: hours(3))
        ])

        XCTAssertEqual(
            data.hero,
            .nextJob(orderId: "confirmed", status: ._2, startsAt: hours(2), whereLine: "Jana · Praha 5")
        )
    }

    func testConfirmedOnTheWayAndInProgressQualifyAndNoOtherStatusDoes() {
        for (code, status) in [(2, OrderStatus._2), (3, ._3), (4, ._4)] {
            let data = from(preview: Self.board, upcoming: [.upcoming(id: "job", status: code, startsAt: hours(1))])
            XCTAssertEqual(data.hero, .nextJob(orderId: "job", status: status, startsAt: hours(1), whereLine: nil))
        }
        for code in [0, 1, 5, 6] {
            let data = from(preview: Self.board, upcoming: [.upcoming(id: "job", status: code, startsAt: hours(1))])
            XCTAssertEqual(data.hero, .availableWork(jobCount: 3, potentialEarnings: 900), "status \(code)")
        }
        XCTAssertEqual(from(preview: nil, upcoming: []).hero, .empty)
    }

    /// Android's `nextJobWhereLine`: the customer and the address, or whichever of them is there.
    func testTheWhereLineFallsBackToWhicheverHalfIsThere() {
        XCTAssertEqual(whereLine(name: "Jana", address: "Praha 5"), "Jana · Praha 5")
        XCTAssertEqual(whereLine(name: "Jana", address: "  "), "Jana")
        XCTAssertEqual(whereLine(name: nil, address: "Praha 5"), "Praha 5")
        XCTAssertNil(whereLine(name: " ", address: nil))
    }

    func testTodaysJobsCountCountsOnlyUpcomingDatedToday() {
        let data = from(preview: nil, upcoming: [
            .upcoming(id: "this-morning", status: 4, startsAt: hours(-2)),
            .upcoming(id: "tonight", status: 2, startsAt: hours(9)),
            .upcoming(id: "unconfirmed-today", status: 0, startsAt: hours(4)),
            .upcoming(id: "tomorrow", status: 2, startsAt: hours(24)),
            .upcoming(id: "yesterday", status: 4, startsAt: hours(-24)),
            .upcoming(id: "undated", status: 2, startsAt: nil)
        ])

        XCTAssertEqual(data.todaysJobsCount, 3)
    }

    private func from(preview: AvailableJobsPreview?, upcoming: [OrderListItem]) -> DashboardData {
        DashboardData.from(stats: .stub(), preview: preview, upcoming: upcoming, firstName: nil, now: Self.now)
    }

    private func whereLine(name: String?, address: String?) -> String? {
        let data = from(preview: nil, upcoming: [
            .upcoming(id: "job", status: 2, startsAt: hours(1), customerName: name, address: address)
        ])
        guard case let .nextJob(_, _, _, whereLine) = data.hero else { return "no next-job hero: \(data.hero)" }
        return whereLine
    }

    private func hours(_ value: Double) -> Date {
        Self.now.addingTimeInterval(value * 3600)
    }
}
