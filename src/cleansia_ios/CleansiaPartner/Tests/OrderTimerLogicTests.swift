import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class OrderTimerLogicTests: XCTestCase {
    private let anchor = Date(timeIntervalSince1970: 1_700_000_000)

    private func order(
        status: Int,
        scheduledOffset: TimeInterval? = nil,
        history: [(Int, TimeInterval)] = [],
        completedAtOffset: TimeInterval? = nil
    ) -> OrderDetail {
        var item = OrderItem()
        item.orderStatus = Code(value: status)
        item.cleaningDateTime = scheduledOffset.map { anchor.addingTimeInterval($0) }
        item.completedAt = completedAtOffset.map { anchor.addingTimeInterval($0) }
        item.statusHistory = history.map {
            OrderStatusTrackDto(status: Code(value: $0.0), createdOn: anchor.addingTimeInterval($0.1))
        }
        return OrderDetail(item)
    }

    // MARK: - Completed duration (the deliberate divergence from Android)

    /// Android computes `now - startedAt` for Completed, so re-opening an old
    /// order inflates the number every time. iOS anchors both ends: the answer is
    /// the same a week later.
    func testCompletedDurationUsesCompletedAtNotNow() {
        let detail = order(
            status: 5,
            history: [(4, 0.0)],
            completedAtOffset: 95 * 60
        )
        let phase = OrderTimer.phase(for: detail, now: anchor.addingTimeInterval(7 * 24 * 3600))
        XCTAssertEqual(
            phase,
            .completed(durationMinutes: 95, finishedAt: anchor.addingTimeInterval(95 * 60))
        )
    }

    func testCompletedFallsBackToTheLastCompletedHistoryEntryWhenCompletedAtIsNil() {
        let detail = order(status: 5, history: [(4, 0.0), (5, 3600.0), (5, 5400.0)])
        XCTAssertEqual(
            OrderTimer.phase(for: detail, now: anchor.addingTimeInterval(50 * 3600)),
            .completed(durationMinutes: 90, finishedAt: anchor.addingTimeInterval(90 * 60))
        )
    }

    func testCompletedWithNoStartedEntryYieldsNilDuration() {
        let detail = order(status: 5, completedAtOffset: 95 * 60)
        XCTAssertEqual(
            OrderTimer.phase(for: detail, now: anchor),
            .completed(durationMinutes: nil, finishedAt: anchor.addingTimeInterval(95 * 60))
        )
    }

    func testCompletedDurationClampsNegativeToNil() {
        let detail = order(status: 5, history: [(4, 600.0)], completedAtOffset: 60)
        XCTAssertEqual(
            OrderTimer.phase(for: detail, now: anchor),
            .completed(durationMinutes: nil, finishedAt: anchor.addingTimeInterval(60))
        )
    }

    func testCompletedWithNeitherAnchorRendersNoCard() {
        XCTAssertNil(OrderTimer.phase(for: order(status: 5), now: anchor))
    }

    func testStartedAtTakesTheEarliestInProgressStamp() {
        let history = [
            OrderStatusTrackDto(status: Code(value: 4), createdOn: anchor.addingTimeInterval(600)),
            OrderStatusTrackDto(status: Code(value: 3), createdOn: anchor),
            OrderStatusTrackDto(status: Code(value: 4), createdOn: anchor.addingTimeInterval(120))
        ]
        XCTAssertEqual(OrderTimer.startedAt(history), anchor.addingTimeInterval(120))
    }

    // MARK: - Live phases

    func testElapsedCountsFromTheStartStamp() {
        let detail = order(status: 4, history: [(4, 0.0)])
        XCTAssertEqual(
            OrderTimer.phase(for: detail, now: anchor.addingTimeInterval(3725)),
            .elapsed(seconds: 3725)
        )
    }

    func testInProgressWithoutAStartStampRendersNoCard() {
        XCTAssertNil(OrderTimer.phase(for: order(status: 4), now: anchor))
    }

    func testCountdownFlipsToScheduledOnceTheStartTimePasses() {
        let detail = order(status: 2, scheduledOffset: 3600)
        XCTAssertEqual(
            OrderTimer.phase(for: detail, now: anchor),
            .countdown(secondsRemaining: 3600)
        )
        XCTAssertEqual(
            OrderTimer.phase(for: detail, now: anchor.addingTimeInterval(3600)),
            .scheduled(anchor.addingTimeInterval(3600))
        )
        XCTAssertEqual(
            OrderTimer.phase(for: detail, now: anchor.addingTimeInterval(7200)),
            .scheduled(anchor.addingTimeInterval(3600))
        )
    }

    func testConfirmedWithoutAScheduledTimeRendersNoCard() {
        XCTAssertNil(OrderTimer.phase(for: order(status: 2), now: anchor))
    }

    func testConfirmedHeadlineSwitchesToSoonInsideThirtyMinutes() {
        XCTAssertFalse(OrderTimer.isHeadingOutSoon(secondsRemaining: 30 * 60))
        XCTAssertTrue(OrderTimer.isHeadingOutSoon(secondsRemaining: 30 * 60 - 1))
        XCTAssertTrue(OrderTimer.isHeadingOutSoon(secondsRemaining: 0))
        XCTAssertFalse(OrderTimer.isHeadingOutSoon(secondsRemaining: -1))
    }

    func testOnTheWayShowsTheScheduledArrival() {
        XCTAssertEqual(
            OrderTimer.phase(for: order(status: 3, scheduledOffset: 900), now: anchor),
            .arriving(anchor.addingTimeInterval(900))
        )
    }

    func testNoCardForNewPendingOrCancelled() {
        for status in [0, 1, 6] {
            XCTAssertNil(
                OrderTimer.phase(
                    for: order(status: status, scheduledOffset: 3600, history: [(4, 0.0)], completedAtOffset: 600),
                    now: anchor
                ),
                "status \(status) should render no timer card"
            )
        }
    }

    // MARK: - Formatting

    func testElapsedClockFormats() {
        XCTAssertEqual(OrderTimer.elapsedClock(seconds: 0), "00:00")
        XCTAssertEqual(OrderTimer.elapsedClock(seconds: 8), "00:08")
        XCTAssertEqual(OrderTimer.elapsedClock(seconds: 128), "02:08")
        XCTAssertEqual(OrderTimer.elapsedClock(seconds: 6128), "1:42:08")
        XCTAssertEqual(OrderTimer.elapsedClock(seconds: -5), "00:00")
    }
}
