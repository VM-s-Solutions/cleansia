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

/// The one place a wire payload becomes a card. Everything downstream of it is pinned in
/// CleansiaCoreTests; this pins the crossing.
@available(iOS 16.1, *)
final class CleanOrderCardModelTests: XCTestCase {
    private let now = Date(timeIntervalSince1970: 1_700_000_000)

    private func state(_ status: String, phaseStart: Date? = nil, phaseEnd: Date? = nil) -> CleanOrderAttributes
        .ContentState
    {
        CleanOrderAttributes.ContentState(
            v: 1,
            status: status,
            orderNumber: "ORD-AB12CD34",
            scheduledStart: now.addingTimeInterval(-1800),
            scheduledEnd: now.addingTimeInterval(1800),
            phaseStart: phaseStart,
            phaseEnd: phaseEnd
        )
    }

    func testEveryStatusCrossesIntoItsCard() {
        XCTAssertEqual(state("onTheWay").cardModel(now: now).card, .journey(.onTheWay))
        XCTAssertEqual(state("inProgress").cardModel(now: now).card, .journey(.cleaning))
        XCTAssertEqual(state("completed").cardModel(now: now).card, .journey(.done))
        XCTAssertEqual(state("cancelled").cardModel(now: now).card, .cancelled)
        XCTAssertEqual(state("v2Rescheduled").cardModel(now: now).card, .unknown)
    }

    func testTheClockIsTheActualProjectionWhenThereIsOneAndTheBookedEndOtherwise() {
        let projected = now.addingTimeInterval(2700)

        XCTAssertEqual(state("inProgress", phaseEnd: projected).cardModel(now: now).legEnd, projected)
        XCTAssertEqual(state("inProgress").cardModel(now: now).legEnd, now.addingTimeInterval(1800))
    }

    /// The number the owner saw. While the cleaner is on the way the card's instant is their expected
    /// ARRIVAL — strictly earlier than the clean's booked end — so the card must caption it as one. Under a
    /// "Finish" caption it says a two-hour clean ends the moment the cleaner rings the bell.
    func testTheOnTheWayClockIsTheArrivalAndIsCaptionedAsOne() {
        let arrival = now.addingTimeInterval(600)
        let onTheWay = state("onTheWay", phaseStart: now.addingTimeInterval(-300), phaseEnd: arrival)
        let model = onTheWay.cardModel(now: now)

        XCTAssertEqual(model.legEnd, arrival)
        XCTAssertLessThan(model.legEnd, onTheWay.scheduledEnd)
        XCTAssertEqual(model.card.timeCaption, .arrival)
    }

    func testTheCleaningClockIsTheFinishAndIsCaptionedAsOne() {
        let finish = now.addingTimeInterval(2700)
        let model = state("inProgress", phaseStart: now.addingTimeInterval(-300), phaseEnd: finish)
            .cardModel(now: now)

        XCTAssertEqual(model.legEnd, finish)
        XCTAssertEqual(model.card.timeCaption, .finish)
    }

    func testAnInServiceCardCarriesALiveWindowAnchoredNoLaterThanNow() throws {
        let range = try XCTUnwrap(state("inProgress").cardModel(now: now).liveRange)

        XCTAssertLessThanOrEqual(range.lowerBound, now)
        XCTAssertGreaterThan(range.upperBound, now)
    }

    /// A finished or cancelled clean has nothing to time. The booked window it still carries can easily sit
    /// in the future (a tomorrow booking cancelled today), so this must be decided by the STATUS — a live
    /// bar creeping across a dead order's card is the failure this guards.
    func testATerminalCardCarriesNoLiveWindowEvenWhenItsBookedWindowIsStillAhead() {
        let tomorrow = now.addingTimeInterval(86400)
        for status in ["completed", "cancelled"] {
            let ahead = CleanOrderAttributes.ContentState(
                v: 1,
                status: status,
                orderNumber: "ORD-AB12CD34",
                scheduledStart: tomorrow,
                scheduledEnd: tomorrow.addingTimeInterval(7200),
                phaseStart: nil,
                phaseEnd: nil
            )

            XCTAssertNil(ahead.cardModel(now: now).liveRange, "\(status) kept a live window")
            XCTAssertNil(ahead.cardModel(now: now).card.timeCaption, "\(status) still promises a time")
        }
    }

    func testTheOrderLabelCarriesTheNumberAndFallsBackWhenThereIsNone() {
        XCTAssertTrue(state("inProgress").cardModel(now: now).orderLabel.contains("ORD-AB12CD34"))

        var anonymous = state("inProgress")
        anonymous.orderNumber = ""

        XCTAssertFalse(anonymous.cardModel(now: now).orderLabel.isEmpty)
        XCTAssertFalse(anonymous.cardModel(now: now).orderLabel.contains("#"))
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

    /// Confirmed has no phase to time — the appointment is not an arrival estimate, and treating the
    /// confirmation as the start of a journey is what made the card count down to a booking days out.
    func testAConfirmedOrderCarriesNoPhaseWindowAtAll() {
        let window = EtaWindow.forOrder(order(
            statusValue: 2,
            cleaningDateTime: booked,
            history: [OrderFixtures.track(statusValue: 2, createdOn: booked.addingTimeInterval(-2 * 86400))]
        ))

        XCTAssertNil(window?.phaseStart)
        XCTAssertNil(window?.phaseEnd)
        XCTAssertEqual(window?.countdownEnd, booked.addingTimeInterval(90 * 60))
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
