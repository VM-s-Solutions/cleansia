import ActivityKit
import CleansiaCore
import CleansiaCustomerApi
import Foundation
import XCTest
@testable import CleansiaCustomer

final class LiveActivityEtaTests: XCTestCase {
    private let now = Date(timeIntervalSince1970: 1_700_000_000)

    private func window(
        start: Date,
        end: Date,
        phaseStart: Date? = nil,
        phaseEnd: Date? = nil
    ) -> EtaWindow {
        EtaWindow(scheduledStart: start, scheduledEnd: end, phaseStart: phaseStart, phaseEnd: phaseEnd)
    }

    private func presentation(_ window: EtaWindow, terminalLabel: String? = nil) -> EtaPresentation {
        LiveActivityEta.presentation(window: window, terminalLabel: terminalLabel, now: now)
    }

    func testFutureWindowCountsDownFromNowSoTheDigitsTickOnTheFirstFrame() {
        let eta = presentation(window(start: now + 3600, end: now + 7200))

        XCTAssertEqual(eta, .countdown(now ... now + 7200))
    }

    func testWindowInProgressCountsDownFromItsOwnStart() {
        let eta = presentation(window(start: now - 1800, end: now + 1800))

        XCTAssertEqual(eta, .countdown(now - 1800 ... now + 1800))
    }

    func testFullyElapsedWindowCountsUpFromTheStartInsteadOfFreezingAtZero() {
        let eta = presentation(window(start: now - 7200, end: now - 3600))

        XCTAssertEqual(eta, .elapsed(since: now - 7200))
    }

    func testEndInsideTheFloorCountsUpRatherThanStartADoomedCountdown() {
        let eta = presentation(window(start: now - 3600, end: now + LiveActivityEta.countdownFloor - 1))

        XCTAssertEqual(eta, .elapsed(since: now - 3600))
    }

    func testInvertedWindowNeverProducesACountdown() {
        let eta = presentation(window(start: now - 600, end: now - 3600))

        XCTAssertEqual(eta, .elapsed(since: now - 600))
    }

    func testDegenerateWindowInThePastCountsUpFromIt() {
        let eta = presentation(window(start: now - 600, end: now - 600))

        XCTAssertEqual(eta, .elapsed(since: now - 600))
    }

    func testElapsedAnchorIsNeverInTheFuture() {
        let eta = presentation(window(start: now + 3600, end: now - 60))

        XCTAssertEqual(eta, .elapsed(since: now))
    }

    func testNilPhaseFieldsFallBackToTheBookedWindow() {
        let eta = presentation(window(start: now - 600, end: now + 3600, phaseStart: nil, phaseEnd: nil))

        XCTAssertEqual(eta, .countdown(now - 600 ... now + 3600))
    }

    func testPhaseWindowWinsOverTheBookedOne() {
        let eta = presentation(window(
            start: now - 7200,
            end: now - 3600,
            phaseStart: now - 600,
            phaseEnd: now + 1800
        ))

        XCTAssertEqual(eta, .countdown(now - 600 ... now + 1800))
    }

    func testTerminalLabelWinsOverAnyWindow() {
        let eta = presentation(window(start: now - 600, end: now + 3600), terminalLabel: "Clean complete")

        XCTAssertEqual(eta, .label("Clean complete"))
    }

    func testCountdownEndPrefersTheActualFinishOverTheBookedOne() {
        XCTAssertEqual(window(start: now, end: now + 3600, phaseEnd: now + 5400).countdownEnd, now + 5400)
        XCTAssertEqual(window(start: now, end: now + 3600, phaseEnd: now + 60).countdownEnd, now + 60)
        XCTAssertEqual(window(start: now, end: now + 3600).countdownEnd, now + 3600)
    }

    func testCountdownEndIsTheUpperBoundOfTheCountdownItProduces() {
        let live = window(start: now - 600, end: now + 3600, phaseEnd: now + 1800)

        XCTAssertEqual(presentation(live), .countdown(now - 600 ... live.countdownEnd))
    }

    /// A countdown whose range is inverted or already behind "now" is drawn by the system as a frozen or
    /// placeholder card. No window may produce one.
    func testNoWindowEverProducesAnInvertedOrAlreadyElapsedCountdown() {
        let offsets: [TimeInterval] = [-7200, -3600, -600, -60, 0, 30, 60, 600, 3600, 7200]

        for startOffset in offsets {
            for endOffset in offsets {
                let eta = presentation(window(start: now + startOffset, end: now + endOffset))
                guard case let .countdown(range) = eta else { continue }
                XCTAssertLessThanOrEqual(range.lowerBound, now)
                XCTAssertLessThan(range.lowerBound, range.upperBound)
                XCTAssertGreaterThan(range.upperBound, now)
            }
        }
    }
}

/// The final content-state an ended activity is left showing (ADR-0029 D2). Ending with no content at all
/// leaves the card on its last in-service state — which is the "black box with a spinner" on completion.
@available(iOS 16.1, *)
final class LiveActivityTerminalStateTests: XCTestCase {
    private let booked = Date(timeIntervalSince1970: 1_700_000_000)

    private func inProgressState() -> CleanOrderAttributes.ContentState {
        CleanOrderAttributes.ContentState(
            v: 1,
            status: "inProgress",
            orderNumber: "1042",
            scheduledStart: booked,
            scheduledEnd: booked.addingTimeInterval(90 * 60),
            phaseStart: booked.addingTimeInterval(10 * 60),
            phaseEnd: booked.addingTimeInterval(100 * 60)
        )
    }

    func testTerminalStateCarriesTheTerminalStatus() {
        XCTAssertEqual(inProgressState().terminal(.completed).status, "completed")
        XCTAssertEqual(inProgressState().terminal(.cancelled).status, "cancelled")
    }

    func testTerminalStateClearsThePhaseWindowSoNothingKeepsTiming() {
        let final = inProgressState().terminal(.completed)

        XCTAssertNil(final.phaseStart)
        XCTAssertNil(final.phaseEnd)
    }

    func testTerminalStateKeepsTheIdentityAndBookedWindowOfTheCardItReplaces() {
        let final = inProgressState().terminal(.cancelled)

        XCTAssertEqual(final.v, 1)
        XCTAssertEqual(final.orderNumber, "1042")
        XCTAssertEqual(final.scheduledStart, booked)
        XCTAssertEqual(final.scheduledEnd, booked.addingTimeInterval(90 * 60))
    }

    /// The widget resolves a terminal status to a label, never to a timer — at any "now", including one
    /// long past the booked window the ended card still carries.
    func testATerminalStateAlwaysPresentsTheLabelBranch() {
        let final = inProgressState().terminal(.completed)

        for hours in [-2.0, 0.0, 1.0, 5.0] {
            let eta = LiveActivityEta.presentation(
                window: final.etaWindow,
                terminalLabel: "Clean complete",
                now: booked.addingTimeInterval(hours * 3600)
            )

            XCTAssertEqual(eta, .label("Clean complete"))
        }
    }

    func testATerminalStatesWindowFallsBackToTheBookedOne() {
        XCTAssertEqual(
            inProgressState().terminal(.completed).etaWindow.countdownEnd,
            booked.addingTimeInterval(90 * 60)
        )
    }

    @available(iOS 16.2, *)
    func testTheEndedCardCarriesNoStaleDate() {
        let content = terminalActivityContent(from: inProgressState(), status: .completed)

        XCTAssertNil(content.staleDate)
        XCTAssertEqual(content.state.status, "completed")
        XCTAssertNil(content.state.phaseEnd)
    }

    @available(iOS 16.2, *)
    func testACancelledCardLeavesAtOnceAndACompletedOneLingers() {
        let now = Date(timeIntervalSince1970: 1_700_000_000)

        XCTAssertEqual(LiveActivityPolicy.dismissal(for: .cancelled, now: now).uiPolicy, .immediate)
        XCTAssertEqual(
            LiveActivityPolicy.dismissal(for: .completed, now: now).uiPolicy,
            .after(now.addingTimeInterval(30 * 60))
        )
    }
}

/// ADR-0029 A2: the phase pair is optional in BOTH directions. The wire shape itself is owned by
/// src/Cleansia.Tests/Functions/Fixtures/live-activity-content-state.json; these pin the Swift side of it.
@available(iOS 16.1, *)
final class CleanOrderContentStateWireTests: XCTestCase {
    private let legacy = """
    {"v":1,"status":"inProgress","orderNumber":"ORD-AB12CD34",
     "scheduledStart":"2026-07-20T09:00:00+00:00","scheduledEnd":"2026-07-20T11:00:00+00:00"}
    """

    private let withPhase = """
    {"v":1,"status":"inProgress","orderNumber":"ORD-AB12CD34",
     "scheduledStart":"2026-07-20T09:00:00+00:00","scheduledEnd":"2026-07-20T11:00:00+00:00",
     "phaseStart":"2026-07-20T09:30:00+00:00","phaseEnd":"2026-07-20T11:30:00+00:00"}
    """

    private func decode(_ json: String) throws -> CleanOrderAttributes.ContentState {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(CleanOrderAttributes.ContentState.self, from: Data(json.utf8))
    }

    func testAPayloadBuiltBeforeThePhaseFieldsStillDecodes() throws {
        let state = try decode(legacy)

        XCTAssertNil(state.phaseStart)
        XCTAssertNil(state.phaseEnd)
        XCTAssertEqual(state.orderNumber, "ORD-AB12CD34")
    }

    func testThePhaseFieldsDecodeAndReachTheEtaWindow() throws {
        let state = try decode(withPhase)

        XCTAssertEqual(state.etaWindow.phaseStart, state.scheduledStart.addingTimeInterval(30 * 60))
        XCTAssertEqual(state.etaWindow.phaseEnd, state.scheduledEnd.addingTimeInterval(30 * 60))
    }
}

final class OrderEtaWindowTests: XCTestCase {
    private let booked = Date(timeIntervalSince1970: 1_700_000_000)

    private func order(
        statusValue: Int,
        cleaningDateTime: Date? = nil,
        estimatedTime: Int? = 90,
        history: [OrderStatusTrackDto]? = nil
    ) -> OrderItem {
        OrderItem(
            id: "o1",
            cleaningDateTime: cleaningDateTime,
            estimatedTime: estimatedTime,
            orderStatus: Code(type: "OrderStatus", name: nil, value: statusValue),
            statusHistory: history
        )
    }

    func testWindowIsNilWithoutAnAppointmentTime() {
        XCTAssertNil(EtaWindow.forOrder(order(statusValue: 4)))
    }

    func testBookedWindowIsTheAppointmentPlusTheEstimate() {
        let window = EtaWindow.forOrder(order(statusValue: 3, cleaningDateTime: booked))

        XCTAssertEqual(window?.scheduledStart, booked)
        XCTAssertEqual(window?.scheduledEnd, booked.addingTimeInterval(90 * 60))
    }

    func testInProgressProjectsTheFinishFromTheActualStart() {
        let startedLate = booked.addingTimeInterval(40 * 60)
        let window = EtaWindow.forOrder(order(
            statusValue: 4,
            cleaningDateTime: booked,
            history: [OrderFixtures.track(statusValue: 4, createdOn: startedLate)]
        ))

        XCTAssertEqual(window?.phaseStart, startedLate)
        XCTAssertEqual(window?.phaseEnd, startedLate.addingTimeInterval(90 * 60))
    }

    func testOnTheWayCountsToTheExpectedArrival() {
        let leftAt = booked.addingTimeInterval(-20 * 60)
        let window = EtaWindow.forOrder(order(
            statusValue: 3,
            cleaningDateTime: booked,
            history: [OrderFixtures.track(statusValue: 3, createdOn: leftAt)]
        ))

        XCTAssertEqual(window?.phaseStart, leftAt)
        XCTAssertEqual(window?.phaseEnd, booked)
    }

    func testALateDepartureStillLeavesRunwayToArrive() {
        let leftAt = booked.addingTimeInterval(30 * 60)
        let window = EtaWindow.forOrder(order(
            statusValue: 3,
            cleaningDateTime: booked,
            history: [OrderFixtures.track(statusValue: 3, createdOn: leftAt)]
        ))

        XCTAssertEqual(window?.phaseEnd, leftAt.addingTimeInterval(10 * 60))
    }

    func testAVeryShortEstimateStillLeavesRunwayToClean() {
        let startedAt = booked.addingTimeInterval(5 * 60)
        let window = EtaWindow.forOrder(order(
            statusValue: 4,
            cleaningDateTime: booked,
            estimatedTime: 2,
            history: [OrderFixtures.track(statusValue: 4, createdOn: startedAt)]
        ))

        XCTAssertEqual(window?.phaseEnd, startedAt.addingTimeInterval(10 * 60))
    }

    func testNoHistoryLeavesThePhaseFieldsNil() {
        let window = EtaWindow.forOrder(order(statusValue: 4, cleaningDateTime: booked, history: []))

        XCTAssertNil(window?.phaseStart)
        XCTAssertNil(window?.phaseEnd)
    }

    func testAMissingEstimateStillYieldsAValidWindow() {
        let window = EtaWindow.forOrder(order(statusValue: 3, cleaningDateTime: booked, estimatedTime: nil))

        XCTAssertEqual(window?.scheduledEnd, booked.addingTimeInterval(60))
    }

    /// The freeze this fixes: the booked window is long gone, but the clean actually started 10 minutes
    /// ago — the card must still count DOWN to the real finish, not sit at 00:00.
    func testACleanThatStartedLateStillCountsDown() throws {
        let now = booked.addingTimeInterval(3 * 3600)
        let window = try XCTUnwrap(EtaWindow.forOrder(order(
            statusValue: 4,
            cleaningDateTime: booked,
            history: [OrderFixtures.track(statusValue: 4, createdOn: now.addingTimeInterval(-600))]
        )))

        XCTAssertEqual(
            LiveActivityEta.presentation(window: window, terminalLabel: nil, now: now),
            .countdown(now.addingTimeInterval(-600) ... now.addingTimeInterval(80 * 60))
        )
    }
}
