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
    ) throws -> OrderDetail {
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: status)
        item.cleaningDateTime = scheduledOffset.map { anchor.addingTimeInterval($0) }
        item.completedAt = completedAtOffset.map { anchor.addingTimeInterval($0) }
        item.statusHistory = history.map {
            OrderStatusTrackDto(status: Code(value: $0.0), createdOn: anchor.addingTimeInterval($0.1))
        }
        return try OrderDetail(item)
    }

    // MARK: - Completed duration (the deliberate divergence from Android)

    /// Android computes `now - startedAt` for Completed, so re-opening an old
    /// order inflates the number every time. iOS anchors both ends: the answer is
    /// the same a week later.
    func testCompletedDurationUsesCompletedAtNotNow() throws {
        let detail = try order(
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

    func testCompletedFallsBackToTheLastCompletedHistoryEntryWhenCompletedAtIsNil() throws {
        let detail = try order(status: 5, history: [(4, 0.0), (5, 3600.0), (5, 5400.0)])
        XCTAssertEqual(
            OrderTimer.phase(for: detail, now: anchor.addingTimeInterval(50 * 3600)),
            .completed(durationMinutes: 90, finishedAt: anchor.addingTimeInterval(90 * 60))
        )
    }

    func testCompletedWithNoStartedEntryYieldsNilDuration() throws {
        let detail = try order(status: 5, completedAtOffset: 95 * 60)
        XCTAssertEqual(
            OrderTimer.phase(for: detail, now: anchor),
            .completed(durationMinutes: nil, finishedAt: anchor.addingTimeInterval(95 * 60))
        )
    }

    func testCompletedDurationClampsNegativeToNil() throws {
        let detail = try order(status: 5, history: [(4, 600.0)], completedAtOffset: 60)
        XCTAssertEqual(
            OrderTimer.phase(for: detail, now: anchor),
            .completed(durationMinutes: nil, finishedAt: anchor.addingTimeInterval(60))
        )
    }

    func testCompletedWithNeitherAnchorRendersNoCard() throws {
        XCTAssertNil(try OrderTimer.phase(for: order(status: 5), now: anchor))
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

    func testElapsedCountsFromTheStartStamp() throws {
        let detail = try order(status: 4, history: [(4, 0.0)])
        XCTAssertEqual(
            OrderTimer.phase(for: detail, now: anchor.addingTimeInterval(3725)),
            .elapsed(seconds: 3725)
        )
    }

    func testInProgressWithoutAStartStampRendersNoCard() throws {
        XCTAssertNil(try OrderTimer.phase(for: order(status: 4), now: anchor))
    }

    func testCountdownFlipsToScheduledOnceTheStartTimePasses() throws {
        let detail = try order(status: 2, scheduledOffset: 3600)
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

    func testConfirmedWithoutAScheduledTimeRendersNoCard() throws {
        XCTAssertNil(try OrderTimer.phase(for: order(status: 2), now: anchor))
    }

    func testConfirmedHeadlineSwitchesToSoonInsideThirtyMinutes() {
        XCTAssertFalse(OrderTimer.isHeadingOutSoon(secondsRemaining: 30 * 60))
        XCTAssertTrue(OrderTimer.isHeadingOutSoon(secondsRemaining: 30 * 60 - 1))
        XCTAssertTrue(OrderTimer.isHeadingOutSoon(secondsRemaining: 0))
        XCTAssertFalse(OrderTimer.isHeadingOutSoon(secondsRemaining: -1))
    }

    func testOnTheWayShowsTheScheduledArrival() throws {
        XCTAssertEqual(
            try OrderTimer.phase(for: order(status: 3, scheduledOffset: 900), now: anchor),
            .arriving(anchor.addingTimeInterval(900))
        )
    }

    func testNoCardForNewPendingOrCancelled() throws {
        for status in [0, 1, 6] {
            XCTAssertNil(
                try OrderTimer.phase(
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

    // MARK: - Tracker

    /// The bar under the timer text is the second half of the same hero block, and it was a
    /// re-derivation rather than a port: four segments where Android has five, no step counter, no
    /// current phase, and Cancelled rendering as an all-muted bar indistinguishable from a brand new
    /// order. These pin it against `ContinuousProgressBar.kt`.
    func testTheTrackerCarriesFivePhasesNotFour() {
        XCTAssertEqual(OrderTrackerProgress.stepCount, 5)
        for status in OrderStatus.allCases where status != ._6 {
            guard case let .steps(segments, _) = OrderTrackerProgress.state(for: status) else {
                return XCTFail("\(status) dropped the segmentation")
            }
            XCTAssertEqual(segments.count, 5, "\(status) renders \(segments.count) segments")
        }
    }

    func testNewAndPendingSitOnTheFirstPhase() {
        for status in [OrderStatus._0, ._1] {
            XCTAssertEqual(
                OrderTrackerProgress.state(for: status),
                .steps(segments: [.current, .future, .future, .future, .future], stepNumber: 1)
            )
        }
    }

    func testEachLiveStatusAdvancesExactlyOnePhase() {
        XCTAssertEqual(
            OrderTrackerProgress.state(for: ._2),
            .steps(segments: [.past, .current, .future, .future, .future], stepNumber: 2)
        )
        XCTAssertEqual(
            OrderTrackerProgress.state(for: ._3),
            .steps(segments: [.past, .past, .current, .future, .future], stepNumber: 3)
        )
        XCTAssertEqual(
            OrderTrackerProgress.state(for: ._4),
            .steps(segments: [.past, .past, .past, .current, .future], stepNumber: 4)
        )
    }

    /// Completed fills every phase rather than leaving the last one sweeping — a finished job must not
    /// read as still working, which is exactly what an `index <= step` fill produces.
    func testCompletedFillsEveryPhase() {
        XCTAssertEqual(
            OrderTrackerProgress.state(for: ._5),
            .steps(segments: [.past, .past, .past, .past, .past], stepNumber: 5)
        )
    }

    func testCancelledDropsTheSegmentation() {
        XCTAssertEqual(OrderTrackerProgress.state(for: ._6), .cancelled)
    }

    func testExactlyOnePhaseSweepsUntilTheJobIsDone() {
        for status in OrderStatus.allCases where status != ._5 && status != ._6 {
            guard case let .steps(segments, _) = OrderTrackerProgress.state(for: status) else {
                return XCTFail("\(status) dropped the segmentation")
            }
            XCTAssertEqual(
                segments.filter { $0 == .current }.count,
                1,
                "\(status) has no single sweeping phase"
            )
        }
        guard case let .steps(segments, _) = OrderTrackerProgress.state(for: ._5) else {
            return XCTFail("Completed dropped the segmentation")
        }
        XCTAssertFalse(segments.contains(.current), "a completed job still sweeps")
    }

    /// Through the BUILT bundle, not the `.xcstrings` source: a key present in the catalog but absent
    /// from a shipped `.lproj` renders as its own name on screen.
    func testTheStepCounterResolvesInEveryLanguage() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for language in ["en", "cs", "sk", "uk", "ru"] {
            L10n.bundle = try localeBundle(language)
            let rendered = L10n.Orders.trackerStepCounter(3, 5)
            XCTAssertNotEqual(rendered, "tracker_step_counter", "unresolved in \(language)")
            XCTAssertTrue(rendered.contains("3"), "\(language) drops the step: \(rendered)")
            XCTAssertTrue(rendered.contains("5"), "\(language) drops the total: \(rendered)")
        }
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
